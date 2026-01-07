# DND Projekt

> **VIGTIGT – Contribution & Commit Information**  
> GitHub commit history afspejler **ikke fuldt ud den reelle arbejdsfordeling** i projektet.  
> Selvom størstedelen af commits er foretaget af brugeren **Litepac**, har **alle gruppemedlemmer bidraget aktivt** til udvikling, design, implementering og dokumentation af systemet.
>
> En detaljeret redegørelse for den faktiske arbejdsfordeling kan findes her:  
> **[commits.md](./commits.md)**

**Gruppe:**

- Oliver Rolighed (OliRolig)
- Oliver Hyllested (Olihylle)
- Sebastian Højland (Sebastianhoejland)
- Rasmus Møller (Litepac aka the goat)

## SystemKrav

- **.NET 8 SDK**

Tjek version:

~~~bash
dotnet --version
- det kan være den kun viser version 10.0, men når man har downloadet 8.0 virker det.
~~~

---

## Database – VIGTIG FORUDSÆTNING

 Projektet kræver adgang til en SQL Server-database baseret på en .bak-fil.

For at systemet fungerer korrekt, skal følgende være opfyldt:

- Du skal have filen stena.bak
- Filen skal restores i SQL Server Management Studio (SSMS)
- Connection string i API-projektet skal pege på den restored database

Uden denne database:

- API’et vil ikke kunne starte korrekt
- ML-funktionalitet og dashboards vil ikke fungere

Start projektet lokalt

- Åbn to terminaler i repoets rodkatalog.
- Start altid API’en først.

---

## Start projektet lokalt

Åbn **to terminaler** i repoets rodkatalog — start altid **API’en først**.

### API – ASP.NET Core API

~~~bash
cd DNDProject.Api
dotnet clean
dotnet restore
dotnet build
dotnet run
~~~

Når API’en er startet, skriver den adressen i konsollen (typisk `http://localhost:5230/`).

### WEB – Blazor WebAssembly Frontend

Åbn en **ny** terminal:

~~~bash
cd DNDProject.Web
dotnet clean
dotnet restore
dotnet build
dotnet run
~~~

Web skriver selv adressen i konsollen (typisk noget som `http://localhost:5122/`). Åbn den i browseren.

## Projektoversigt

Projektet består af:

- SP.NET Core REST API
- Blazor WebAssembly frontend
- JWT-baseret autentifikation
- Maskinlæringskomponenter til anbefalinger
- SQL Server database baseret på SSMS

Arkitekturen er opdelt i klare lag med fokus på:

- Sikkerhed
- Skalerbarhed
- Vedligeholdelse
- Separation of concerns

## ## Dokumentation

Projektets design og implementering er dokumenteret gennem følgende blogposts:

- **[Blogpost 1 – Project Setup & Architecture](./blogpost1.md)**
- **[Blogpost 2 – Web Service Design & Implementation](./blogpost2.md)**
- **[Blogpost 3 – Authentication & Authorization](./blogpost3.md)**
- **[Blogpost 4 – Machine Learning & Recommendations](./blogpost4.md)**
- **[Blogpost 5 – Frontend Integration](./blogpost5.md)**
- **[Blogpost 6 – Project Conclusion & Demonstration](./blogpost6.md)**

Alle blogposts er skrevet i Markdown og er en del af den samlede projektdokumentation.
