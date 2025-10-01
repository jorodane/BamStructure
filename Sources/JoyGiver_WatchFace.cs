using RimWorld;

namespace BamStructure
{
    public class JoyGiver_WatchFace : JoyGiver_WatchBuilding
    {
        //아름다운 가진 애만 쓰게 할까 고민을 하고 있는 중인데 그건 또 아닌 것 같기도 하고 뭔가 뭔가
        //public override Job TryGiveJob(Pawn pawn)
        //{
        //    if (pawn.GetStatValue(StatDefOf.Beauty) < 0.5f) return null;
        //    return base.TryGiveJob(pawn);
        //}
    }
}
