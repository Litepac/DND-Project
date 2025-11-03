namespace DNDProject.Api.Models
{
    public enum StenaReceiptKind
    {
        Unknown = 0,
        Weight = 1,        // Enhed = KG
        Emptying = 2       // Enhed = STK (tømning)
    }

    public class StenaReceipt
    {
        public int Id { get; set; }

        // hvornår det skete (hvis vi kunne læse det)
        public DateTime? Date { get; set; }

        // varenummer, fx 710100 / 832495
        public string? ItemNumber { get; set; }

        // “Småt brændbart affald”, “Tømning 1100L minicontainer”, ...
        public string? ItemName { get; set; }

        // KG / STK
        public string? Unit { get; set; }

        // selve tallet fra "Antal"
        public double? Amount { get; set; }

        // EAK kode hvis vi fandt den
        public string? EakCode { get; set; }

        // hvilken type vi har udledt (weight vs emptying)
        public StenaReceiptKind Kind { get; set; }

        // for Emptying: “1100L”, “16m3”, “minicontainer” – udledt fra teksten
        public string? ContainerTypeText { get; set; }

        // vi gemmer hvilket Excel det kom fra
        public string? SourceFile { get; set; }

        // hvis der en dag kommer container-id i filen
        public string? RawContainer { get; set; }
    }
}
