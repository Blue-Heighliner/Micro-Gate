namespace BlueHeighliner.MicroGate;

/// <summary>
/// The line encoding a MicroGate device uses for transmitted and received data.
/// </summary>
public enum MicroGateEncoding
{
    /// <summary>
    /// Non-return-to-zero.
    /// </summary>
    Nrz,

    /// <summary>
    /// Non-return-to-zero, inverted level.
    /// </summary>
    Nrzb,

    /// <summary>
    /// Non-return-to-zero inverted, with a mark (1) causing a transition.
    /// </summary>
    NrziMark,

    /// <summary>
    /// Non-return-to-zero inverted, with a space (0) causing a transition.
    /// </summary>
    NrziSpace,

    /// <summary>
    /// Biphase mark.
    /// </summary>
    BiphaseMark,

    /// <summary>
    /// Biphase space.
    /// </summary>
    BiphaseSpace,

    /// <summary>
    /// Biphase level.
    /// </summary>
    BiphaseLevel,

    /// <summary>
    /// Differential biphase level.
    /// </summary>
    DifferentialBiphaseLevel,
}
