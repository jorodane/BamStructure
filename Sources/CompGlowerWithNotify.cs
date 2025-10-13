using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyBuilder;
using UnityEngine;
using Verse;

namespace BamStructure
{
    public class CompProperties_GlowerWithNotify : CompProperties_Glower
    {
        public CompProperties_GlowerWithNotify() => compClass = typeof(CompGlowerWithNotify);
    }

    public class CompGlowerWithNotify : CompGlower
    {
        public event System.Action OnColorChanged;
        protected override void SetGlowColorInternal(ColorInt? color)
        {
            base.SetGlowColorInternal(color);
            OnColorChanged?.Invoke();
        }
    }

    public class Building_GlowerColored : Building
    {
        CompGlowerWithNotify asGlower;
        public override Color DrawColorTwo
        {
            get
            {
                if (asGlower == null) return base.DrawColorTwo;
                else return asGlower.GlowColor.ToColor + Color.black;
            }
        }

        public void InitGlow()
        {
            if (this.TryGetComp(out asGlower))
            {
                asGlower.OnColorChanged -= Notify_ColorChanged;
                asGlower.OnColorChanged += Notify_ColorChanged;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            InitGlow();
        }
    }
}
