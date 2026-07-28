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
        "....KKKKKKK...............",
        "..KK2222222KKK............",
        ".K22233333333KK...........",
        ".K2233344444443KK.........",
        "K223344445555443KK........",
        "K22334445556666544K.......",
        "K2233444555666665443K.....",
        "K223344455566666554 3K....",
        "K22334445555KKKKKKKKKKK...",
        "K2233444555Kdddddddddd K..",
        "K223344455 KKKKKKKKKKKK...",
        "K22334445556666554 3K.....",
        "K2233344455555443 2K......",
        "K22233344444333 22K.......",
        ".K222333333333 2K.........",
        ".K22233333322 K...........",
        "..K122222222 K............",
        "..K111222221K.............",
        "...KKKKKKKKK..............",
    };

    /// <summary>Red plume, drawn to overlap the crown so it reads as mounted.</summary>
    public static readonly string[] Plume =
    {
        "....KKKKK.......",
        "..KKCCCCBK......",
        ".KCCCBBBBRK.....",
        "KCCBBBBRRRK.....",
        "KCBBBRRRRrK.....",
        "KBBBRRRRrrK.....",
        "KBBRRRRrrrK.....",
        "KBRRRRrrrrK.....",
        "KRRRRrrrrrK.....",
        ".KRRrrrrrrK.....",
        ".KRrrrrrrrK.....",
        "..Krrrrrr K.....",
        "..Krrrrr K......",
        "...Krrr K.......",
        "...KrrK.........",
        "....KK..........",
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
        "...K33444445 K....",
        "...KKKKKKKKKK.....",
    };

    /// <summary>
    /// Folded wing sweeping up and back. Feathers are separated by dark quill
    /// lines, without which the whole thing collapses into a white blob.
    /// </summary>
    public static readonly string[] WingFolded =
    {
        "............KKKK....",
        "..........KK6666K...",
        "........KK666655K...",
        "......KK66665554K...",
        ".....K666655544K4K..",
        "....K66655544K444K..",
        "...K6655544K4444vK..",
        "..K665544K44444vvK..",
        "..K65544K4444vvvK...",
        ".K6554K44444vvvK....",
        ".K554K4444vvvvK.....",
        "K54K44444vvvK.......",
        "K4K4444vvvvK........",
        ".K4444vvvK..........",
        ".K444vvvK...........",
        "..K44vvK............",
        "..K4vvK.............",
        "...KKK..............",
    };

    /// <summary>Wing part-open, used while airborne.</summary>
    public static readonly string[] WingOpen =
    {
        "..........KKKKKK....",
        "......KKKK666666K...",
        "...KKK66666665554K..",
        "KKK6666666555544K4K.",
        "K66666655554444K44K.",
        "K6666555544 4K444vK.",
        "K665555444K4444vvK..",
        "K6555444K44444vvK...",
        "K554 4K44444vvvK....",
        "K54K444444vvvK......",
        "K4K44444vvvvK.......",
        ".K44444vvvK.........",
        ".K4444vvvK..........",
        "..K44vvvK...........",
        "..K44vvK............",
        "...K4vK.............",
        "...KKK..............",
        "....................",
    };

    /// <summary>Wing fully spread, used on take-off.</summary>
    public static readonly string[] WingSpread =
    {
        ".....KKKKKKKKKK.....",
        "..KKK6666666666K....",
        "KKK666666666655K4K..",
        "K66666666655554K44K.",
        "K666666655544 K444K.",
        "K6666555444 K4444vK.",
        "K66555444 K44444vvK.",
        "K6555444K444444vvK..",
        "K555 4K4444444vvK...",
        "K54 K44444444vvK....",
        "K4K4444444vvvvK.....",
        "K K444444vvvK.......",
        ".K44444vvvK.........",
        ".K4444vvvK..........",
        "..K444vvK...........",
        "..K44vvK............",
        "...K4vK.............",
        "...KKK..............",
    };

    /// <summary>Cape at rest, hanging with a ragged hem.</summary>
    public static readonly string[] CapeRest =
    {
        "...KKKKKKKK...",
        "..KCBBBBBBCK..",
        ".KCBBRRRRBBCK.",
        ".KBBRRrrRRBBK.",
        "KBBRRrrrrRRBK.",
        "KBRRrrrrrrRBK.",
        "KBRRrrrrrrRBK.",
        "KBRrrrrrrrRBK.",
        "KBRrrrrrrrRBK.",
        "KBRrrrrrrrRBK.",
        "KRRrrrrrrrRBK.",
        "KRrrrrrrrrRBK.",
        "KRrrrrrrrrRK..",
        "KRrrrrrrrrRK..",
        "KRrrrrrrrRK...",
        "KRrrrrrrrRK...",
        ".KrrrrrrrRK...",
        ".KrrKrrrKRK...",
        ".KrK.KrK.KK...",
    };

    /// <summary>Cape drifting, used at walking speed.</summary>
    public static readonly string[] CapeDrift =
    {
        "....KKKKKKKK..",
        "...KCBBBBBBCK.",
        "..KCBBRRRRBBCK",
        "..KBBRRrrRRBBK",
        ".KBBRRrrrrRRBK",
        ".KBRRrrrrrrRBK",
        "KBRRrrrrrrRBK.",
        "KBRrrrrrrrRBK.",
        "KBRrrrrrrrRBK.",
        "KRRrrrrrrrRBK.",
        "KRrrrrrrrrRK..",
        "KRrrrrrrrRK...",
        "KRrrrrrrRK....",
        "KRrrrrrRK.....",
        "KrrrrrRK......",
        "KrrrrRK.......",
        "KrrrRK........",
        "KrKrK.........",
        "KK.KK.........",
    };

    /// <summary>Cape streaming flat, used when dashing or airborne.</summary>
    public static readonly string[] CapeStream =
    {
        "........KKKKKK",
        "....KKKKCBBBBK",
        "KKKKCBBBBRRRRK",
        "KCBBBRRRrrrrRK",
        "KBRRrrrrrrrrRK",
        "KBRrrrrrrrrRK.",
        "KRRrrrrrrrRK..",
        "KRrrrrrrrRK...",
        "KRrrrrrrRK....",
        "KrrrrrrRK.....",
        "KrrrrrRK......",
        "KrrrrRK.......",
        "KrrrRK........",
        "KrrRK.........",
        "KrRK..........",
        "KRK...........",
        "KK............",
        "..............",
        "..............",
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
