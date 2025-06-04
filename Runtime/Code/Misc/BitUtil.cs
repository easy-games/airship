namespace Code.Misc {
    public static class BitUtil {
        public static bool GetBit(byte bools, int bit) => (bools & (1 << bit)) != 0;
        public static bool GetBit(short bools, int bit) => (bools & (1 << bit)) != 0;

        public static void SetBit(ref byte bools, int bit, bool value)
        {
            if (value)
                bools |= (byte)(1 << bit);
            else
                bools &= (byte)~(1 << bit);
        }
        
        public static void SetBit(ref short bools, int bit, bool value)
        {
            if (value)
                bools |= (short)(1 << bit);
            else
                bools &= (short)~(1 << bit);
        }
    }
}