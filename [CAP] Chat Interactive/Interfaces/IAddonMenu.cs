// IAddonMenu.cs
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Interfaces
{
    public interface IAddonMenu
    {
        List<FloatMenuOption> MenuOptions();
    }
}