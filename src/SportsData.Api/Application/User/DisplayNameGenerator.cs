namespace SportsData.Api.Application.User
{
    public static class DisplayNameGenerator
    {
        private static readonly string[] Adjectives = new[]
        {
            "gritty", "fearless", "relentless", "lockedin", "scrappy",
            "quickfooted", "unpredictable", "hungry", "wild", "sneaky",
            "clever", "efficient", "precise", "unguardable", "stickyhanded",
            "clutch", "coolheaded", "ghostlike"
        };

        private static readonly string[] Animals = new[]
        {
            "aardvark", "alpaca", "armadillo", "bat", "blobfish", "capybara",
            "chameleon", "clam", "crab", "dolphin", "eel", "gecko", "hamster",
            "jellyfish", "koala", "lemur", "lizard", "manta", "mole", "narwhal",
            "newt", "octopus", "otter", "pangolin", "platypus", "porcupine",
            "pufferfish", "quokka", "salamander", "seahorse", "shrimp", "sloth",
            "snail", "squid", "starfish", "tapir", "tardigrade", "toad",
            "turtle", "wallaby", "weasel", "wombat", "yak"
        };

        public static string Generate()
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            var adjective = Adjectives[random.Next(Adjectives.Length)];
            var animal = Animals[random.Next(Animals.Length)];
            return $"{adjective}_{animal}";
        }
    }
}