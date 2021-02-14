using System;

namespace WpfApp1
{
    internal class Livery
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsRejected { get; set; }
        public bool IsCustomNumber { get; set; }
        public string LiveryType { get; set; }
    }
}