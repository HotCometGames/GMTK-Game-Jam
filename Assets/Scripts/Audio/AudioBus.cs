/// <summary>
/// Which filtered voice pool a sound plays through.
///
/// The whole feel of the game's audio comes from EQing these two families in OPPOSITE
/// directions, so a stock click and a stock thud read as a deliberate pair:
///   Crisp   — high-passed. Everything below ~300Hz is rolled off so pops feel light,
///             bright and dry (jump, dash, level advance, UI). Think lichess piece-drop.
///   Cushion — low-passed. Everything above ~4.5kHz is rolled off so impacts feel soft,
///             rounded and weighted (landing, death), and never compete with the pops.
///   Raw     — unfiltered escape hatch for anything that shouldn't be coloured.
/// </summary>
public enum AudioBus
{
    Crisp,
    Cushion,
    Raw
}
