namespace LXGaming.Docker.Cli.Models {

    public class Choice {

        public string Id { get; init; }

        public string Name { get; init; }

        public override string ToString() {
            return Name;
        }
    }
}