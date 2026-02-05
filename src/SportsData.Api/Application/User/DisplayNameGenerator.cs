namespace SportsData.Api.Application.User
{
    public static class DisplayNameGenerator
    {
        private static readonly string[] Adjectives = new[]
        {
            "aggressive", "agile", "blazing", "clever", "clutch",
            "coolheaded", "dominant", "dynamic", "efficient", "electric",
            "epic", "explosive", "fearless", "fierce", "ghostlike",
            "gritty", "heroic", "hungry", "hyper", "legendary",
            "lockedin", "majestic", "mega", "mighty", "nitro",
            "noble", "powerful", "precise", "quickfooted", "raging",
            "relentless", "rocket", "royal", "ruthless", "savage",
            "scrappy", "sneaky", "strategic", "super", "swift",
            "tactical", "thunderous", "turbo", "ultra", "unguardable",
            "unpredictable", "unstoppable", "wild"
        };

        private static readonly string[] Animals = new[]
        {
            "aardvark", "alpaca", "armadillo", "badger", "bandit",
            "barracuda", "bat", "bear", "beaver", "bison",
            "blobfish", "buccaneer", "buffalo", "capybara", "chameleon",
            "cheetah", "clam", "cobra", "condor", "cougar",
            "coyote", "crab", "crusader", "dolphin", "dragon",
            "eagle", "eel", "falcon", "fox", "gazelle",
            "gecko", "gladiator", "grizzly", "hamster", "hawk",
            "hornet", "husky", "jaguar", "jellyfish", "koala",
            "lemur", "leopard", "lizard", "lynx", "manta",
            "mantis", "marauder", "mole", "mongoose", "mustang",
            "narwhal", "newt", "nomad", "octopus", "osprey",
            "otter", "pangolin", "panther", "patriot", "phoenix",
            "platypus", "porcupine", "pufferfish", "python", "quokka",
            "raider", "ranger", "raptor", "raven", "reaper",
            "rebel", "renegade", "rhino", "ronin", "salamander",
            "samurai", "scorpion", "seahorse", "sentinel", "shark",
            "shrimp", "sloth", "snail", "spartan", "squid",
            "stallion", "starfish", "tapir", "tardigrade", "tiger",
            "titan", "toad", "trojan", "turtle", "viper",
            "vulture", "wallaby", "warrior", "weasel", "wolf",
            "wolverine", "wombat", "yak"
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