using System;

namespace Client.Simulation.Menu
{
    public sealed class DemoCatalog
    {
        private readonly string[] _addresses;

        public DemoCatalog(string[] addresses)
        {
            if (addresses == null)
                throw new ArgumentNullException(nameof(addresses));

            _addresses = (string[])addresses.Clone();
        }

        public int Count => _addresses.Length;

        public string this[int index] => _addresses[index];
    }
}
