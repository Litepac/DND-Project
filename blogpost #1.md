# Blogpost 1 – Project Formulation & Initial Requirements

## Project domain and motivation

Vores semesterprojekt tager udgangspunkt i et fiktivt, men realistisk, produkt kaldet **Stena Smart Collection**, inspireret af problemstillinger inden for affaldsindsamling og ressourceoptimering.

Domænet handler om **data-drevet optimering af containeropsætning og tømningsfrekvens** hos erhvervskunder. I dag baseres mange beslutninger om afhentning stadig på faste ruter, erfaring eller manuelle vurderinger, hvilket ofte fører til enten overfyldte containere eller unødvendige tømninger.

Formålet med projektet er at udvikle en **webbaseret applikation**, der giver:

- overblik over kunders historiske data  
- indsigt i ineffektiv afhentning  
- datadrevne anbefalinger (via ML) til forbedret opsætning  

Vi valgte dette domæne, fordi det:

- er tæt koblet til virkelige forretnings- og logistikproblemer  
- giver mulighed for at arbejde med **.NET Web API, Blazor WebAssembly og dataanalyse**  
- kombinerer klassisk CRUD-funktionalitet med mere avanceret logik og anbefalinger  

Applikationen er bygget som en **.NET-baseret løsning** med et API-backend og en Blazor WebAssembly frontend.

---

## Overordnet systemidé

Systemet giver brugeren (fx en intern medarbejder hos Stena Recycling) mulighed for at:

- logge ind i systemet  
- se et samlet dashboard over kunder  
- analysere tømningsmønstre og fyldningsgrader  
- identificere ineffektiv afhentning  
- få ML-baserede anbefalinger til containerstørrelse, antal og tømningsfrekvens  
- sammenligne nuværende setup med et foreslået setup og se potentielle besparelser  

Applikationen er opdelt i tydelige funktionelle områder:

- **Kunde-dashboard**
- **Tømningsanalyse**
- **ML-anbefaling**
- **Manuel beregning og justering**

---

## Initial requirements (user stories)

På baggrund af domænet har vi formuleret følgende indledende krav som user stories:

### Autentifikation

- *Som bruger vil jeg kunne logge ind, så kun autoriserede personer har adgang til data.*

### Kundeoverblik

- *Som bruger vil jeg kunne se en liste over alle kunder med nøgletal, så jeg hurtigt kan identificere relevante kunder.*  
- *Som bruger vil jeg kunne vælge en kunde og se detaljerede historiske data.*

### Tømningsanalyse

- *Som bruger vil jeg kunne analysere tømningsdata for en kunde, så jeg kan se hvor ofte containere er over- eller underfyldte.*  
- *Som bruger vil jeg kunne filtrere data over en given periode, så analysen er fleksibel.*

### ML-baseret anbefaling

- *Som bruger vil jeg kunne få en anbefalet containeropsætning baseret på historiske data.*  
- *Som bruger vil jeg kunne sammenligne nuværende opsætning med en ML-anbefaling.*  
- *Som bruger vil jeg kunne justere den anbefalede opsætning manuelt.*

### Overblik og effekt

- *Som bruger vil jeg kunne se estimerede besparelser i ture, CO₂ og omkostninger, så værdien af ændringerne er tydelig.*

---

## Afgrænsning

Projektet fokuserer på **analyse, visualisering og anbefaling**. Funktioner som ruteplanlægning, eksterne afhentningsplaner og realtidsintegration er bevidst udeladt for at holde projektets scope realistisk inden for semesterets rammer.
