using System;
using Abstractions;

namespace Domain.Profil
{
    public partial class Profil : IState
    {
        public string Name { get; set; } = string.Empty;
    }
}