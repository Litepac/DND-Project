# Logbog

## Mandag d. 22/09

Snakket om requirements.

---

## Tirsdag d. 23/09

Implementeret **Blazor** osv.

---

## Mandag d. 29/09

Møde om webservices.  
Sat **containers** op, **CURS** op, testet **ApiPing** og diverse andre ting.  
Sat **UI på**, database opdateret, samt kigget på absurd mange ting.

---

## Tirsdag d. 30/09

Kigget yderligere på **tokens og login**.  
Det er meget kringlet, men det giver tildels mening — der sker dog mange ting.

Samlet **packages** i roden, så `.csproj`-filer i de forskellige folders har nemmere ved at gå derop.  
Der skete **version mismatch** mange steder, så det her giver mening at gøre.

---

## Mandag d. 03/11

Fokus på **Stena-dashboardet** i Blazor WebApp.  
Importerede data fra Stena-Excel-filer til databasen (over 600.000 rækker).  
Alt gemmes i tabellen `StenaReceipts`.

Oprettet ny **StenaController** i API’et med følgende endpoints:

- `/api/stena/dashboard` -> samlet overblik med KPI’er  
- `/api/stena/weights/daily` -> vægt pr. dag (seneste X dage)  
- `/api/stena/emptyings/top` -> top-typer af tømninger  
- `/api/stena/weights/latest` -> seneste vægtlinjer

I Blazor-delen oprettet **StenaDashboard.razor**, der viser:

- KPI-kort (antal rækker, total vægt, antal tømninger)  
- Vægt pr. dag og top-tømninger som interaktive visninger  
- Fane-system mellem *Oversigt* og *Rå data*  
- Søgefelt og EAK-filtrering i tabellen

Tilføjet ekstra **CSS-styling** for kort, tabeller og små grafer.  
Hele pipeline fra **Excel → Database → API → Blazor Dashboard** virker nu.  

Der mangler dog stadig at blive tilføjet rigtige grafer og bedre interaktivitet, men data og funktionalitet er på plads.

---

## Næste skridt

- Implementere **grafisk visualisering** (fx Chart.js eller MudBlazor Charts)  
- Tilføje **datointerval-vælger** (fra/til-dato)  
- Forbedre **interaktivitet** i dashboardet (klikbare elementer, drilldown, filterfunktioner)  
- Optimere **performance** ved store datasæt (lazy loading / pagination)  
- Udvide integration med **containerdata** fra pilot-dashboardet  
- Gøre UI’et mere “app-agtigt” med bedre layout og responsive komponenter.

# Logbog – Uge 50

Ugens arbejde fokuserede på Stena-delen af systemet: database­forståelse, API-rettelser og opbygning af analysevisningen for tømningseffektivitet. Der blev arbejdet både i backend og frontend, og flere dele af infrastrukturen kom på plads.

## Database og modeller

- `Kapacitet_og_enhed_opdateret` blev analyseret og mappet korrekt til EF Core.
- Modellen **ContainerCapacity** blev opdateret, så den matcher tabellens faktiske kolonner.
- Konflikter i DbContext blev identificeret (flere modeller pegede på samme tabel).
- Mappingen af **StenaReceipt** blev rettet så:
  - `LevNr` → CustomerKey  
  - `Antal` → Amount  
  - `Varenummer` → ItemNumber  
  - øvrige felter nu binder korrekt.

## Backend – API-udvikling og rettelser

To nye endpoints blev bygget til tømningseffektivitet:

- `GET /api/stena/efficiency/summary` – samlet overblik pr. kunde  
- `GET /api/stena/efficiency/customer/{levNr}` – detaljer for én kunde  

Der blev desuden:

- Oprettet nye DTO’er:
  - `ContainerEfficiencySummaryDto`
  - `ContainerEfficiencyDetailDto`
  - `ContainerEmptyingDto`
- Implementeret beregninger for fyldningsgrad, ineffektive tømninger og gennemsnit.
- Rettet fejl i controlleren, bl.a. manglende namespace-referencer og forkerte datatyper.
- Fundet en EF-fejl ved opstart: samme tabel blev mappet af flere modeller.  
  Dette giver en **500 Internal Server Error** og skal løses, før efficiency-API’et kan fungere.

## Frontend – Blazor

- Siden **tømningsanalyse (660 L container)** blev designet og sat op.
- Kunde-dashboardet fungerer nu igen efter API-rettelser.
- Analyse-UI'et viser:
  - kundeliste,
  - valg af periode og tærskel,
  - placeholder for detaljeret visning.
- Siden kan ikke hente data endnu, da API’et returnerer 404/500 pga. manglende færdiggørelse af efficiency-endpoints.

## Status for dagen

- Kunde-dashboard virker.  
- API’et til tømningseffektivitet er næsten færdigt, men fejler ved opstart pga. EF-konflikt.  
- Tømningsanalyse-siden er bygget, men venter på at API’et fungerer.

Når EF-konflikten omkring **ContainerCapacity** er løst, vil både summary-endpointet og kundedetalje-endpointet fungere, og analysevisningen kan kobles helt sammen.