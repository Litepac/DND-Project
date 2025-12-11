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

I uge 50 blev der arbejdet intensivt på at etablere fundamentet for Stena-delen af projektet. Fokus var på at få API’et korrekt koblet til databasen, forstå datagrundlaget og begynde opbygningen af et kunde-dashboard.

## Databaseforbindelse og struktur

- Projektet blev forbundet korrekt til Stena SQL-databasen via EF Core.
- Modtagelsestabellen (`dbo.Modtagelse`) blev analyseret i dybden for at forstå relevante felter.
- Det blev afklaret, at `LevNr` fungerer som det reelle CustomerID.
- Relevante datafelter blev kortlagt:
  - `Varenummer` bestemmer typen af tømning/affald.
  - `Unit = 'KG'` betyder, at `Antal` repræsenterer vægt i kilo.
  - `Unit = 'STK'` bruges til faste tømninger, hvor `Antal` typisk er 1.

## Backend – API-udvikling

Der blev oprettet et nyt sæt API-endpoints til Stena-kundedata:

- `GET /api/stena/customers/summary`  
  Returnerer en oversigt over alle kunder med:
  - Samlet vægt
  - Antal modtagelser
  - Første og sidste modtagelsesdato

- `GET /api/stena/customers/{customerKey}/dashboard`  
  Returnerer detaljer for én kunde, inkl.:
  - Total vægt
  - Antal modtagelser
  - Tidsserie pr. måned (vægtudvikling)

Derudover blev der udviklet logik til:

- Parsing af `Antal` (nvarchar) til decimal — understøtter både dansk og internationalt talformat.
- Aggregation af data pr. kunde og pr. måned.
- DTO’er blev oprettet og stabiliseret:
  - `CustomerSummaryDto`
  - `CustomerDashboardDto`
  - `CustomerTimeseriesPointDto`

## Fejlrettelser

- Rettede kolonnenavnsfejl og EF Core-mapping-issues.
- Løste 500-fejl forårsaget af summering på nvarchar-felter.
- Ensrettede datatyper, herunder `CustomerKey` (string → int).
- Fjernede gamle og fejlbehæftede Stena-controllere, som forhindrede projektet i at bygge.

## Frontend (Blazor)

- Opsatte fuldt dataflow mellem frontend og backend.
- Implementerede første version af et kunde-dashboard-view.
- Tilføjede oversigtsside baseret på summary-endpointet.
- Implementerede fejlvisning og loading-tilstande.

## Overblik

Ved udgangen af uge 50 er grundinfrastrukturen på plads:

- API’et fungerer stabilt og leverer korrekte kundedata.
- Blazor kan hente og visualisere både kundesummaries og detaljer.
- Datamodellen i Stena-tabellen er nu fuldt forstået.
- Alt er klar til at bygge videre på analyser, grafer og KPI’er.
