using Arsenal.Core;
using Players.Interface.Model;
using Ships;

namespace Arsenal.Vehicles
{
    /// <summary>What the local player is flying.</summary>
    internal static class Pilot
    {
        /// <summary>The ship (root of its rigid assembly) the local player is piloting, from any seat but a fire-control station.</summary>
        public static bool TryGetPilotedRoot(out uint rootId) => TryGetSeatRoot(out rootId, out bool station) && !station;

        /// <summary>The ship the local player is crewing: at its controls or at one of its fire-control stations.</summary>
        public static bool TryGetCrewRoot(out uint rootId) => TryGetSeatRoot(out rootId, out _);

        private static bool TryGetSeatRoot(out uint rootId, out bool station)
        {
            rootId = 0;
            station = false;
            if (Svc.PlayersClient == null || !Svc.PlayersClient.TryGetLocalPlayerState(out PlayerState state)) return false;
            if (state.Type != PlayerStateType.Piloting) return false;
            station = GunnerStation.IsStation(state);
            rootId = Svc.ShipsClient.TryGetTrackedRootShip(state.EntityId, out TrackedShipClient root) && root != null ? root.Id : state.EntityId;
            return true;
        }
    }
}
