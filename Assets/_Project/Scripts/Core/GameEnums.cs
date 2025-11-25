using System;

namespace ElitesAndPawns.Core
{
    /// <summary>
    /// The three playable factions in the game
    /// </summary>
    public enum FactionType
    {
        None = 0,
        Blue = 1,   // The Architects - Tactical, deployables, team-focused
        Red = 2,    // The Destroyers - Heavy damage, environmental destruction
        Green = 3   // The Hunters - Mobile, long-range, fragile
    }

    /// <summary>
    /// Alias for FactionType to support different naming conventions
    /// </summary>
    public enum Team
    {
        None = 0,
        Blue = 1,   // The Architects - Tactical, deployables, team-focused
        Red = 2,    // The Destroyers - Heavy damage, environmental destruction
        Green = 3   // The Hunters - Mobile, long-range, fragile
    }

    /// <summary>
    /// Overall state of the game
    /// </summary>
    public enum GameState
    {
        MainMenu,
        WarMapView,      // RTS layer
        BattleLoading,   // Transition to FPS
        InBattle,        // FPS layer
        PostBattle,      // Results screen
        Paused
    }

    /// <summary>
    /// Types of nodes on the war map
    /// </summary>
    public enum NodeType
    {
        MajorCity,      // Victory condition nodes
        ResourcePoint,  // Generate tokens
        StrategicPoint, // Tactical bonuses
        SupplyHub       // Enable reinforcement
    }

    /// <summary>
    /// Game modes for FPS battles
    /// </summary>
    public enum GameMode
    {
        ControlPoints,  // MVP mode
        KingOfTheHill,  // Post-MVP
        CaptureTheFlag, // Post-MVP
        Mixed           // Post-MVP combination
    }
}
