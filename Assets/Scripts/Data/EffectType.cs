namespace TR.Data
{
    // Categories of card effects we may want to cap per arena
    public enum EffectType
    {
        None = 0,
        Slow = 1,
        Stun = 2,
        Burn = 3,
        Poison = 4,
        Frostbite = 5,
        // Add more as needed: Chain, Zap, Tornado, etc.
    }
}
