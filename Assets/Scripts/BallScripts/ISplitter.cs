public interface ISplitter {
    int BallsOnSplit { get; set; }
}

/// <summary>
/// Marks a splitter that Fission should boost: a ball that splits into copies of
/// itself (Scatter, Matryoshka). Projector balls (Confetti, Eye on the Prize) also
/// use <see cref="ISplitter.BallsOnSplit"/> for their own count but spawn OTHER balls,
/// so they implement ISplitter without this marker and Fission leaves them alone.
/// </summary>
public interface IFissionable : ISplitter { }
