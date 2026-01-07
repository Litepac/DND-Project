# Blogpost 3 – Authentication, Authorization & ML-based Recommendations

## Formål

Denne blogpost beskriver, hvordan autentifikation, autorisation og ML-baserede anbefalinger er implementeret i systemet. Fokus er på, hvordan login, roller, sikker adgang, datavisualisering og beslutningsstøtte hænger sammen i en samlet arkitektur, der understøtter både forretningsmæssige og bæredygtige mål.

Blogposten relaterer direkte til følgende krav:

- **FR1** – Login and roles  
- **FR4** – Pickup frequency recommendations  
- **FR5** – CO₂ and cost impact  
- **FR6** – Filtering and search  
- **NFR3 & NFR4** – Security, data quality & plausibility checks  

---

## 1 Login og rollebaseret adgang (FR1, NFR3)

Systemet anvender **JWT-baseret autentifikation** med roller (Admin og Sales). Login håndteres i frontend via en Blazor-komponent, som kalder API’et og gemmer JWT-token i browserens `localStorage`.

### Login (Login.razor)

- Tokenet gemmes client-side og bruges automatisk på alle efterfølgende API-kald via en DelegatingHandler.

```razor
var resp = await Http.PostAsJsonAsync("api/auth/login", model);
var dto = await resp.Content.ReadFromJsonAsync<LoginResponse>();

await AuthState.MarkUserAsAuthenticatedAsync(dto.Token);
Nav.NavigateTo("/home", replace: true);
```

## Token storage

```razor
public sealed class TokenStorage : ITokenStorage
{
    public Task SaveAsync(string token) =>
        _js.InvokeVoidAsync("localStorage.setItem", "auth_token", token).AsTask();

    public Task<string?> GetAsync() =>
        _js.InvokeAsync<string?>("localStorage.getItem", "auth_token").AsTask();
}
```

Dette sikrer, at:

- Brugeren forbliver logget ind ved refresh
- API’et altid kan validere brugerens identitet og rolle

## Rollebaseret UI og navigation (FR1, NFR1)

Navigationen er dynamisk baseret på brugerens claims. Roller og email vises direkte i UI, og logout rydder både token og authentication state.

## NavMenu.razor

```razor
roles = user?.Claims
    .Where(c => c.Type == ClaimTypes.Role)
    .Select(c => c.Value)
    .Distinct()
    .ToList() ?? new List<string>();

@if (roles?.Count > 0)
{
    <div>@string.Join(" • ", roles)</div>
}
```

Dette giver:

- Klar rolle-feedback til brugeren
- Ét fælles navigationspunkt for alle funktioner
- En ensartet brugeroplevelse (NFR1 – usability)

## ML – kundeliste og filtrering (FR4, FR6)

ML-anbefalingerne starter i en oversigtsside, hvor Sales-brugere kan se og filtrere kunder baseret på historiske data og ML-output.

## MLCustomers.razor – datahentning

```razor
var url =
    $"api/ml-viz/customers?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&take={take}&search={search}";

var data = await Http.GetFromJsonAsync<VizResponse>(url);
rows = ApplySort(data.Top);

Debounced søgning
await Task.Delay(350, ct);
await Load();
```

Dette reducerer unødvendige API-kald og forbedrer performance (NFR2).

## Sortering kan ske på

- Total kg
- Gns. kg pr. dag
- Antal dage
- Seneste dato

## ML-anbefaling for én kunde (FR4)

Når en kunde vælges, vises et detaljeret beslutningsview, hvor nuværende setup sammenlignes med ML’ens anbefaling.

## MLCustomer.razor – anbefaling

```razor
var recUrl =
    $"api/ml/recommend-one?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&customerNo={CustomerNo}";

rec = await recResp.Content.ReadFromJsonAsync<RecommendationDto>();
```

ML-outputtet indeholder:

- Anbefalet containerstørrelse
- Antal containere
- Tømningsfrekvens
- Forventet fyldningsgrad
- Brugeren kan manuelt justere værdierne, uden at ML-resultatet overskrives.

## CO₂- og omkostningsberegning (FR5)

For at dokumentere bæredygtig og økonomisk effekt beregnes forskellen mellem nuværende og foreslået setup direkte i UI.

## Beregning af ture/år

```razor
static double TripsPerYear(int frequencyDays)
{
    return 365.0 / Math.Max(1, frequencyDays);
}
```

## CO₂-beregning

```razor
var currentCo2 = currentTrips * kmPerTrip * co2PerKm;
var proposedCo2 = proposedTrips * kmPerTrip * co2PerKm;
```

## Omkostninger

```razor
double RentCostPerYear(Setup s)
    => s.ContainerCount * RentPerMonth(s.ContainerL) * 12.0;
```

Resultatet vises som:

- Færre/flere ture pr. år
- Potentiel CO₂-besparelse (kg/år)
- Potentiel økonomisk gevinst (kr/år)
- Dette understøtter både dokumentation og dialog med kunden.

## Datakvalitet og plausibility checks (NFR4)

Systemet beskytter mod urealistiske anbefalinger gennem:

- Maksimalt antal containere
- Begrænsede containerstørrelser
- Out-of-scope warnings

Eksempel: out-of-scope check
var outOfScope = (Proposed.ContainerL == 1100 && Proposed.ContainerCount >= 15);

Hvis et setup vurderes urealistisk, vises en tydelig advarsel i UI, og brugeren guides mod alternative løsninger.

## Konklusion

Denne del af systemet samler autentifikation, autorisation og ML-baseret beslutningsstøtte i én sammenhængende løsning. Kombinationen af sikre API’er, rollebaseret UI og forklarlige ML-anbefalinger gør det muligt for Sales-brugere at:

Arbejde sikkert og rollebaseret

Identificere ineffektive setups

Dokumentere CO₂- og omkostningseffekter

Understøtte datadrevne anbefalinger over for kunder

Dermed opfylder løsningen både de funktionelle og ikke-funktionelle krav, med fokus på brugervenlighed, sikkerhed og datakvalitet.
