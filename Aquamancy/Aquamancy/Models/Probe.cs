namespace Aquamancy.Models
{
    public enum SignalQuality
    {
        Unknown,
        Excellent,
        Good,
        Fair,
        Weak,
        VeryPoor
    }

    public class Probe
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public int SendFrequencyInSeconds { get; set; } = 60;
        public bool TdsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastNotifiedAt { get; set; }
        public DateTime? LastCommunicationDate { get; set; }
        public DateTime? LastBootedAt { get; set; }
        public int Rssi { get; set; }

        public int ColorR => ConvertHexToRgbComponent(Color, 0);
        public int ColorG => ConvertHexToRgbComponent(Color, 1);
        public int ColorB => ConvertHexToRgbComponent(Color, 2);

        private static int ConvertHexToRgbComponent(string hexColor, int component)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
                return 0;

            // Supprimer le # si présent
            hexColor = hexColor.TrimStart('#');

            // Vérifier que c'est un format valide (6 caractères)
            if (hexColor.Length != 6)
                return 0;

            try
            {
                // Convertir la paire d'hex correspondante en décimal
                int startIndex = component * 2;
                string hexPair = hexColor.Substring(startIndex, 2);
                return Convert.ToInt32(hexPair, 16);
            }
            catch
            {
                return 0;
            }
        }

        public SignalQuality RssiQuality => Rssi switch
        {
            0 => SignalQuality.Unknown,
            >= -50 => SignalQuality.Excellent,
            >= -60 => SignalQuality.Good,
            >= -70 => SignalQuality.Fair,
            >= -80 => SignalQuality.Weak,
            _ => SignalQuality.VeryPoor
        };

        public string RssiQualityLabel => RssiQuality switch
        {
            SignalQuality.Unknown => "Inconnu",
            SignalQuality.Excellent => "Excellent",
            SignalQuality.Good => "Bon",
            SignalQuality.Fair => "Moyen",
            SignalQuality.Weak => "Faible",
            SignalQuality.VeryPoor => "Très faible",
            _ => "Inconnu"
        };

        public string LastCommunicationAgoDisplay => LastCommunicationDate.HasValue
        ? ((int)(DateTime.Now - LastCommunicationDate.Value).TotalMinutes) switch
        {
            < 1 => "1 min",
            <= 120 => $"{(int)(DateTime.Now - LastCommunicationDate.Value).TotalMinutes} min",
            <= 2880 => $"{(int)(DateTime.Now - LastCommunicationDate.Value).TotalHours} heures",
            _ => $"{(int)(DateTime.Now - LastCommunicationDate.Value).TotalDays} jours"
        }
        : "?";
    }
}
