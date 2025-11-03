# DND Projekt

**Gruppe:**

- Oliver Rolighed (OliRolig)
- Oliver Hyllested (Olihylle)
- Sebastian Højland (Sebastianhoejland)
- Rasmus Møller (Litepac aka the goat)

## Hvad kan løsningen?

Systemet består af en ASP.NET Core API og et Blazor WebAssembly-frontend, der arbejder sammen om at hjælpe affaldskunder og Stena-medarbejdere med at vælge de mest passende containere. Løsningen gør det muligt at:

- **Indsamle kundeinput:** Brugere kan registrere mængde og type affald, så systemet får et datagrundlag for beregninger.
- **Anbefale containerløsninger:** På baggrund af input foreslår systemet en passende containerstørrelse og afhentningsfrekvens, så over- og underfyldning undgås.
- **Skabe overblik for kunder:** Kunder kan se historik og udvikling i deres anbefalinger gennem visuelle oversigter, så de bedre kan forstå deres affaldsbehov over tid.
- **Understøtte medarbejdere:** Medarbejdere får et værktøj til at identificere kunder med uhensigtsmæssige containerstørrelser og kan se rapporter om CO₂-besparelser og logistikoptimering.

## SystemKrav

- **.NET 8 SDK**

Tjek version:

~~~bash
dotnet --version
- det kan være den kun viser version 10.0, men når man har downloadet 8.0 virker det.
~~~

---

## Start projektet lokalt

Åbn **to terminaler** i repoets rodkatalog — start altid **API’en først**.

### 1) API – ASP.NET Core API (SQLite)

~~~bash
cd DNDProject.Api
dotnet clean
dotnet restore
dotnet build
dotnet run
~~~

Når API’en er startet, skriver den adressen i konsollen (typisk `http://localhost:5230/`).

### 2) WEB – Blazor WebAssembly Frontend

Åbn en **ny** terminal:

~~~bash
cd DNDProject.Web
dotnet clean
dotnet restore
dotnet build
dotnet run
~~~

Web skriver selv adressen i konsollen (typisk noget som `http://localhost:5122/`). Åbn den i browseren.
