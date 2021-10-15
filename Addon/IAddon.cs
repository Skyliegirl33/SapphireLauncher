using System.Diagnostics;

namespace SapphireBootWPF.Addon {
    public interface IAddon {
        string Name { get; }

        void Setup(Process gameProcess);
    }
}