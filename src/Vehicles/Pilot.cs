using Arsenal.Core;
using Players.Interface.Model;
using Ships;

namespace Arsenal.Vehicles
{
    /// <summary>What the local player is flying.</summary>
    internal static class Pilot
    {
        /// <summary>The ship (root of its rigid assembly) the local player is piloting, from any seat.</summary>
        public static bool TryGetPilotedRoot(out uint rootId)
        {
            rootId = 0;
            if (Svc.PlayersClient == null || !Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState state)) return false;
            if (state.Type != PlayerStateType.Piloting) return false;
            rootId = Svc.ShipsClient.TryGetTrackedRootShip(state.EntityId, out TrackedShipClient root) && root != null ? root.Id : state.EntityId;
            return true;
        }
    }
}
