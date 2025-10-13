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
        public event Action OnColorChanged;
        bool wasLight = false;
        protected override void SetGlowColorInternal(ColorInt? color)
        {
            base.SetGlowColorInternal(color);
            OnColorChanged?.Invoke();
        }

        protected override bool ShouldBeLitNow
        {
            get
            {
                bool shouldBeLight = base.ShouldBeLitNow;
                if (shouldBeLight != wasLight)
                {
                    wasLight = shouldBeLight;
                    OnColorChanged?.Invoke();
                }
                return shouldBeLight;
            }
        }
    }

    public class Building_GlowerColored : Building
    {
        public static Color disabled_Color = Color.black;
        CompGlowerWithNotify asGlower;
        public override Color DrawColorTwo
        {
            get
            {
                if (asGlower == null) return base.DrawColorTwo;
                else return asGlower.Glows ? asGlower.GlowColor.ToColor + Color.black : disabled_Color;
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

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            if (asGlower != null) asGlower.OnColorChanged -= Notify_ColorChanged;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            InitGlow();
        }
    }
}
