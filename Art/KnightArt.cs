using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace CreeperGame.Art;

/// <summary>
/// Hand-authored pixel artwork for the winged knight.
///
/// Every part here is drawn pixel by pixel rather than generated from geometry.
/// Procedural shapes were tried first and always came out soft and generic: an
/// ellipse is an ellipse, and no amount of parameter tuning turns it into a
/// helmet. Placing pixels deliberately is the only way to get a silhouette that
/// reads.
///
/// The parts are separate so they can be posed independently, which is what
/// keeps the animation cost near zero: a new action is a set of joint angles,
/// not a new sheet of artwork.
///
/// Everything is authored facing RIGHT and mirrored when the knight turns.
/// Palette keys are single characters and '.' is transparent.
/// </summary>
public static class KnightArt
{
    /// <summary>
    /// A tight palette. Six steels, four reds, three golds, one void and one
    /// outline. Keeping the count low is most of what makes pixel art cohere.
    /// </summary>
    public static readonly Dictionary<char, Color> Palette = new()
    {
        ['K'] = new Color(12, 11, 16),      // outline
        ['1'] = new Color(34, 36, 48),      // steel, darkest
        ['2'] = new Color(58, 62, 78),
        ['3'] = new Color(94, 100, 120),
        ['4'] = new Color(136, 144, 166),
        ['5'] = new Color(186, 194, 214),
        ['6'] = new Color(236, 241, 250),   // steel, specular
        ['r'] = new Color(54, 11, 17),      // red, darkest
        ['R'] = new Color(100, 21, 29),
        ['B'] = new Color(146, 33, 41),
        ['C'] = new Color(192, 56, 60),     // red, brightest
        ['g'] = new Color(106, 79, 29),     // gold, dark
        ['G'] = new Color(184, 145, 60),
        ['Y'] = new Color(236, 206, 128),   // gold, bright
        ['d'] = new Color(18, 16, 22),      // visor void
        ['w'] = new Color(214, 220, 232),   // feather mid
        ['v'] = new Color(168, 176, 196),   // feather shadow
    };

    /// <summary>
    /// Profile helm facing right.
    ///
    /// The read comes entirely from the outline: a flat-backed skull, a crown
    /// sloping forward, and a pointed snout carrying the visor. Earlier versions
    /// were round and looked like a ball on a stick; the snout is what fixed it.
    /// </summary>
    public static readonly string[] Helmet =
    {
        "...KKKKKK.............",
        ".KK222222KKK..........",
        "K2233333333KK.........",
        "K233344444443KK.......",
        "K23344455555443KK.....",
        "K2334445556666544K....",
        "K233444555666665443K..",
        "K23344455KKKKKKKKKKKK.",
        "K2334445Kdddddddddd K.",
        "K233444 KKKKKKKKKKKK..",
        "K23344455566655 3K....",
        "K2233444555554 2K.....",
        "K223333444433 2K......",
        ".K2223333333 K........",
        ".K11222222 1K.........",
        "..KKKKKKKKK...........",
    };

    /// <summary>Red plume, drawn to overlap the crown so it reads as mounted.</summary>
    public static readonly string[] Plume =
    {
        "...KKKK.......",
        ".KKCCCBK......",
        "KCCBBBBRK.....",
        "KCBBBRRRK.....",
        "KBBRRRRrK.....",
        "KBRRRRrrK.....",
        "KRRRRrrrK.....",
        ".KRRrrrrK.....",
        ".KRrrrrrK.....",
        "..Krrrrr K....",
        "..Krrrr K.....",
        "...Krr K......",
        "...KrK........",
        "....K.........",
    };

    /// <summary>
    /// Breastplate in profile: a narrow shield with a fluted chest, tapering to
    /// the waist, finished with a gold belt and red sash.
    /// </summary>
    public static readonly string[] Torso =
    {
        "...KKKKKKKK.......",
        "..K22233333KK.....",
        ".K2233444444 K....",
        ".K233444555543K...",
        "K23344455566543K..",
        "K2334445556665 3K.",
        "K233444555666654K.",
        "K23344455566665 K.",
        "K233444555566654K.",
        "K23344455556665 K.",
        "K2334445555666 4K.",
        "K233444555566654K.",
        "K23344455556665 K.",
        ".K3344455556654K..",
        ".K334445555665 K..",
        ".K33444555566 4K..",
        ".K3444555556654K..",
        "..K44455555665K...",
        "..KGGGGGGGGGGK....",
        "..KYGGGGGGGGYK....",
        "..KBBBRRRRRBBK....",
        "..KrBBRRRRRBBK....",
        "..K3344455554K....",
        "..K2334445554K....",
        "..K2334445553K....",
        "...K233444553K....",
        "...K23344455K.....",
        "...KKKKKKKKK......",
    };

    /// <summary>
    /// Folded wing sweeping up and back. Feathers are separated by dark quill
    /// lines, without which the whole thing collapses into a white blob.
    /// </summary>
    public static readonly string[] WingFolded =
    {
        "..........KKKKKKK..",
        ".......KKK6666665K.",
        ".....KK666666655vK.",
        "....K66666666655vK.",
        "...K6666666665 5vK.",
        "..K66666666655 vvK.",
        "..K6666666665 5vvK.",
        ".K66K6666665 55vvK.",
        ".K6K466666 555vvvK.",
        "K65K46666 555vvvK..",
        "K6K446665 55vvvvK..",
        "K5K44666 555vvvK...",
        "K5K4466 5555vvvK...",
        ".K44K6 55555vvvK...",
        ".K4K4 555555vvK....",
        ".K4K4555555vvvK....",
        "..K4K55555vvvK.....",
        "..K4K5555vvvvK.....",
        "..K44K555vvvK......",
        "...K4K55vvvK.......",
        "...K4K5vvvK........",
        "...K44Kvv K........",
        "....K4KvvK.........",
        "....K4KvK..........",
        "....K44K...........",
        ".....K4K...........",
        ".....K4K...........",
        "......KK...........",
        "...................",
    };

    /// <summary>Wing part-open, used while airborne.</summary>
    public static readonly string[] WingOpen =
    {
        "........KKKKKKKKKK.",
        "....KKKK666666665K.",
        "..KK66666666666 5vK",
        "KK6666666666665 5vK",
        "K666K66666666 55vvK",
        "K66K466666665 5vvvK",
        "K6K4466666 5555vvvK",
        "K5K44666 55555vvvvK",
        "K5K446 5555555vvvK.",
        ".K44K 55555555vvvK.",
        ".K4K4555555555vvK..",
        ".K4K455555555vvvK..",
        "..K4K5555555vvvK...",
        "..K4K555555vvvK....",
        "..K44K5555vvvK.....",
        "...K4K555vvvK......",
        "...K4K55vvvK.......",
        "...K44Kvv K........",
        "....K4KvvK.........",
        "....K4KvK..........",
        ".....KKK...........",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
    };

    /// <summary>Wing fully spread, used on take-off.</summary>
    public static readonly string[] WingSpread =
    {
        "KKKKKKKKKKKKKKKKKK.",
        "K66666666666666 5vK",
        "K6666666666666 55vK",
        "K666K666666665 5vvK",
        "K66K4666666 5555vvK",
        "K6K44666 5555555vvK",
        "K5K446 55555555vvvK",
        "K5K4 555555555vvvK.",
        ".K4K45555555vvvvK..",
        ".K4K4555555vvvvK...",
        ".K44K55555vvvvK....",
        "..K4K5555vvvvK.....",
        "..K4K555vvvvK......",
        "..K44K55vvvK.......",
        "...K4K5vvvK........",
        "...K4Kvv K.........",
        "...K44KvK..........",
        "....KKK............",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
        "...................",
    };

    /// <summary>Cape at rest, hanging with a ragged hem.</summary>
    public static readonly string[] CapeRest =
    {
        "....KKKKKKKKKK....",
        "..KKCBBBBBBBBCKK..",
        ".KCCBBBRRRRBBBCCK.",
        ".KCBBRRRrrRRRBBCK.",
        "KCBBRRrrrrrRRRBBK.",
        "KBBRRrrrrrrrrRRBK.",
        "KBRRrrrrrrrrrrRBK.",
        "KBRRrrrrrrrrrrRBK.",
        "KBRrrrrrrrrrrrRBK.",
        "KBRrrrrrrrrrrrRBK.",
        "KBRrrrrRRrrrrrRBK.",
        "KBRrrrrRRrrrrrRBK.",
        "KRRrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRK..",
        "KRrrrrrRRrrrrrRK..",
        "KRrrrrrRRrrrrrRK..",
        ".KrrrrrRRrrrrrRK..",
        ".KrrrrrRRrrrrrRK..",
        ".KrrrrrRRrrrrrRK..",
        ".KrrrrrRRrrrrrK...",
        ".KrrrrrRRrrrrrK...",
        "..KrrrrRRrrrrrK...",
        "..KrrrrRRrrrrK....",
        "..KrrrrRRrrrrK....",
        "..KrrrrRRrrrK.....",
        "..KrrrKRRKrrK.....",
        "..KrrK.KK.KrK.....",
        "..KrK......KK.....",
    };

    /// <summary>Cape drifting, used at walking speed.</summary>
    public static readonly string[] CapeDrift =
    {
        "......KKKKKKKKKK..",
        "....KKCBBBBBBBBCK.",
        "...KCCBBBRRRRBBBCK",
        "..KCBBRRRrrRRRBBCK",
        "..KBBRRrrrrrRRRBBK",
        ".KBBRRrrrrrrrrRRBK",
        ".KBRRrrrrrrrrrrRBK",
        "KBRRrrrrrrrrrrRBK.",
        "KBRrrrrrrrrrrrRBK.",
        "KBRrrrrRRrrrrrRBK.",
        "KBRrrrrRRrrrrrRBK.",
        "KRRrrrrRRrrrrrRBK.",
        "KRrrrrrRRrrrrrRK..",
        "KRrrrrrRRrrrrrRK..",
        "KRrrrrRRrrrrrRK...",
        "KRrrrrRRrrrrrRK...",
        "KRrrrRRrrrrrRK....",
        "KRrrrRRrrrrrRK....",
        "KRrrRRrrrrrRK.....",
        "KRrrRRrrrrRK......",
        "KRrRRrrrrrRK......",
        "KRrRRrrrrRK.......",
        "KRRRrrrrRK........",
        "KRRrrrrrRK........",
        "KRrrrrrRK.........",
        "KRrrrrRK..........",
        "KrrrrRK...........",
        "KrrrRK............",
        "KrrRK.............",
        "KrKRK.............",
        "KK.KK.............",
        "..................",
        "..................",
        "..................",
    };

    /// <summary>Cape streaming flat, used when dashing or airborne.</summary>
    public static readonly string[] CapeStream =
    {
        "..........KKKKKKKK",
        "......KKKKCBBBBBBK",
        "..KKKKCBBBBRRRRRRK",
        "KKCBBBBRRRrrrrrrRK",
        "KCBBRRrrrrrrrrrrRK",
        "KBRRrrrrrrrrrrrRK.",
        "KBRrrrrrrrrrrrRK..",
        "KRRrrrrrrrrrrRK...",
        "KRrrrrrrrrrrRK....",
        "KRrrrrrrrrrRK.....",
        "KRrrrrrrrrRK......",
        "KrrrrrrrrRK.......",
        "KrrrrrrrRK........",
        "KrrrrrrRK.........",
        "KrrrrrRK..........",
        "KrrrrRK...........",
        "KrrrRK............",
        "KrrRK.............",
        "KrRK..............",
        "KRK...............",
        "KK................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
    };

    /// <summary>Long straight blade with a gold crossguard, point up.</summary>
    public static readonly string[] Sword =
    {
        "....K....",
        "...K6K...",
        "...K6K...",
        "..K363K..",
        "..K363K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        "..K463K..",
        ".KKKKKKK.",
        "KGYGGGYGK",
        ".KKKKKKK.",
        "...KrK...",
        "...KrK...",
        "...KrK...",
        "..KGYGK..",
        "..KKKKK..",
    };

    /// <summary>Pauldron for the near shoulder, drawn over the arm.</summary>
    public static readonly string[] Pauldron =
    {
        "..KKKKKK..",
        ".K344444K.",
        "K34455554K",
        "K3455666 K",
        "K34556665K",
        "K3445566 K",
        "K33444554K",
        ".K3334443K",
        ".K2333332K",
        "..KKKKKKK.",
    };
}
