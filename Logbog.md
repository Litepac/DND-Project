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
