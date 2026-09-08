using BackyardLegends.Core;

namespace BackyardLegends.Runtime.Network
{
    /// <summary>
    /// Rotates logical seats so the local player's seat is always visual Bottom.
    /// </summary>
    public sealed class SpadesSeatMapper
    {
        public SpadesSeatMapper(SeatId localLogicalSeat)
        {
            LocalLogicalSeat = localLogicalSeat;
        }

        public SeatId LocalLogicalSeat { get; }

        public SeatId ToVisual(SeatId logical)
        {
            var offset = (4 - (int)LocalLogicalSeat) % 4;
            return (SeatId)(((int)logical + offset) % 4);
        }

        public SeatId ToLogical(SeatId visual)
        {
            var offset = (int)LocalLogicalSeat % 4;
            return (SeatId)(((int)visual + offset) % 4);
        }
    }
}
