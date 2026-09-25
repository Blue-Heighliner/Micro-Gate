namespace BlueHeighliner.MicroGate;

/// <summary>
/// The line encoding a MicroGate device uses for transmitted and received data.
/// </summary>
public enum MicroGateEncoding
{
    /// <summary>
    /// Non-return-to-zero.
    /// </summary>
    Nrz = 0,

    /// <summary>
    /// Non-return-to-zero, inverted level.
    /// </summary>
    Nrzb = 1,

    /// <summary>
    /// Non-return-to-zero inverted, with a mark (1) causing a transition.
    /// </summary>
    NrziMark = 2,

    /// <summary>
    /// Non-return-to-zero inverted, with a space (0) causing a transition.
    /// </summary>
    NrziSpace = 3,

    /// <summary>
    /// Biphase mark.
    /// </summary>
    BiphaseMark = 4,

    /// <summary>
    /// Biphase space.
    /// </summary>
    BiphaseSpace = 5,

    /// <summary>
    /// Biphase level.
    /// </summary>
    BiphaseLevel = 6,

    /// <summary>
    /// Differential biphase level.
    /// </summary>
    DifferentialBiphaseLevel = 7,
}
