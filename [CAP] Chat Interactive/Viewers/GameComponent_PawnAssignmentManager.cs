using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    public class GameComponent_PawnAssignmentManager : GameComponent
    {
        private Dictionary<string, string> viewerPawnAssignments; // Username -> ThingID

        public GameComponent_PawnAssignmentManager(Game game)
        {
            viewerPawnAssignments = new Dictionary<string, string>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref viewerPawnAssignments, "viewerPawnAssignments", LookMode.Value, LookMode.Value);

            // Initialize if null after loading
            if (viewerPawnAssignments == null)
                viewerPawnAssignments = new Dictionary<string, string>();
        }

        public void AssignPawnToViewer(string username, Pawn pawn)
        {
            viewerPawnAssignments[username.ToLowerInvariant()] = pawn.ThingID;
        }

        public Pawn GetAssignedPawn(string username)
        {
            if (viewerPawnAssignments.TryGetValue(username.ToLowerInvariant(), out string thingId))
            {
                return FindPawnByThingId(thingId);
            }
            return null;
        }

        public bool HasAssignedPawn(string username)
        {
            if (viewerPawnAssignments.TryGetValue(username.ToLowerInvariant(), out string thingId))
            {
                return FindPawnByThingId(thingId) != null;
            }
            return false;
        }

        public void UnassignPawn(string username)
        {
            viewerPawnAssignments.Remove(username.ToLowerInvariant());
        }

        public IEnumerable<string> GetAllAssignedUsernames()
        {
            return viewerPawnAssignments.Keys.ToList();
        }

        private static Pawn FindPawnByThingId(string thingId)
        {
            if (string.IsNullOrEmpty(thingId))
                return null;

            // Search all maps for the pawn
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.ThingID == thingId)
                        return pawn;
                }
            }

            // Also check world pawns
            var worldPawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.ThingID == thingId);
            return worldPawn;
        }
        public List<Pawn> GetAllViewerPawns()
        {
            var viewerPawns = new List<Pawn>();
            foreach (var thingId in viewerPawnAssignments.Values)
            {
                var pawn = FindPawnByThingId(thingId);
                if (pawn != null && !pawn.Dead)
                {
                    viewerPawns.Add(pawn);
                }
            }
            return viewerPawns;
        }

        public bool IsViewerPawn(Pawn pawn)
        {
            return viewerPawnAssignments.Values.Contains(pawn.ThingID);
        }

        public string GetUsernameForPawn(Pawn pawn)
        {
            var entry = viewerPawnAssignments.FirstOrDefault(x => x.Value == pawn.ThingID);
            return entry.Key ?? null;
        }
    }
}