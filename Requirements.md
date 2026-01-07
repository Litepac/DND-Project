# Requirements – Implementeringsstatus

Dette dokument beskriver de funktionelle og ikke-funktionelle krav for projektet og angiver,
i hvilket omfang disse krav er blevet implementeret i den endelige løsning.

Kravene er oprindeligt udarbejdet i forbindelse med semesterprojektet i samarbejde med
Stena Recycling og har dannet grundlag for systemets design, arkitektur og funktionalitet.

---

## 4.2 Functional Requirements (User Stories)

### FR1 – Login og roller  

**Status: Implementeret**

Systemet understøtter login med personlige brugerkonti og rollebaseret adgang (Sales og Admin).
Autentifikation er implementeret ved hjælp af JWT (JSON Web Tokens) og ASP.NET Identity.

Roller anvendes både i backend og frontend til at styre adgang til funktionalitet, og
brugere kan kun tilgå systemet, hvis de er autentificerede.

---

### FR2 – Kunde- og containeroversigt

**Status: Implementeret**

Sales-brugere har adgang til et kunde-dashboard, som viser en oversigt over kunder,
deres historiske data, tilknyttede containere samt afhentningsinformation.

Dashboardet fungerer som det primære arbejdsredskab for salgsbrugere og giver et samlet
overblik over kundedata og udvikling over tid.

---

### FR3 – Ineffektive tømninger (containerstørrelse)

*Status: Implementeret*

Systemet identificerer kunder med ineffektive tømninger, baseret på fyldningsgrader
under en fastlagt tærskel (standard 80 %).

Resultaterne visualiseres i frontend og gør det muligt for salgsbrugere hurtigt at
identificere kunder, hvor containerstørrelse eller opsætning kan optimeres.

---

### FR4 – Anbefaling af tømningsfrekvens

Status: Implementeret*

Systemet anvender historiske data og maskinlæringsbaserede beregninger til at foreslå
alternative tømningsfrekvenser for udvalgte kunder.

Anbefalingerne kan justeres manuelt i frontend og fungerer som et beslutningsstøtteværktøj
for salgsbrugere.

---

### FR5 – CO₂- og omkostningspåvirkning

**Status: Implementeret**

For hver anbefaling beregner systemet estimerede ændringer i:

- Antal ture pr. år
- CO₂-udledning
- Økonomiske omkostninger

Beregningerne er baseret på konfigurerbare antagelser og vises direkte i brugergrænsefladen
for at understøtte dokumentation af både økonomiske og bæredygtige konsekvenser.

---

### FR6 – Filtrering og søgning

**Status: Implementeret**

Systemet understøtter søgning og filtrering af kunder baseret på blandt andet:

- Kundenavn eller kundenummer
- Periode
- Containerstørrelse
- Effektivitet og fyldningsgrad

Dette gør det muligt for salgsbrugere at analysere specifikke kundesegmenter og fokusere
på relevante cases.

---

### FR7 – Brugeradministration

**Status: Delvist implementeret**

Brugere og roller er teknisk understøttet via ASP.NET Identity, og roller anvendes aktivt
i systemet.

Et fuldt administrationsinterface til oprettelse, redigering og deaktivering af brugere
blev dog ikke færdigimplementeret inden projektets afslutning. Dette er en kendt
afgrænsning, som ikke påvirker systemets kernefunktionalitet.

---

## 4.3 Non-functional Requirements

### NFR1 – Brugervenlighed

**Status: Implementeret**

Systemet er opbygget med fokus på enkel navigation og tydelig visuel struktur.
Brugere kan udføre centrale opgaver uden behov for omfattende oplæring.

---

### NFR2 – Performance og skalerbarhed

**Status: Delvist implementeret**

Systemet har acceptable svartider ved de anvendte datamængder og under normal brug.
Der er ikke gennemført egentlige stresstests eller formel skaleringstest.

---

### NFR3 – Sikkerhed og adgangskontrol

**Status: Implementeret**

Systemet anvender sikker autentifikation via JWT og rollebaseret adgangskontrol.
Kun autoriserede brugere kan tilgå API’et og frontend-applikationen.

---

### NFR4 – Datakvalitet og plausibilitet

**Status: Delvist implementeret**

Systemet håndterer og markerer urealistiske eller ekstreme værdier (outliers) i data,
så disse ikke giver misvisende resultater.

Et fuldt workflow for manuel godkendelse eller rettelse af data er ikke implementeret.

---

### NFR5 – Interoperabilitet

**Status: Implementeret**

Systemet anvender SQL Server som database, administreret via SQL Server Management Studio (SSMS),
og tilgår data gennem Entity Framework Core.

Dette muliggør struktureret og standardiseret dataadgang uden behov for manuel
reformatering.

---

## Samlet vurdering

Projektet opfylder hovedparten af de oprindeligt definerede krav.
De krav, som kun er delvist implementeret, er bevidst afgrænset på grund af tid
og prioritering og påvirker ikke systemets mulighed for at demonstrere
kernefunktionalitet og overordnet arkitektur.
