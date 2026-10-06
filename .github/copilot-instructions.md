# GitHub Copilot Instructions for (NWN) Real Fog of War (Continued)

## Mod Overview and Purpose
The (NWN) Real Fog of War mod is an enhancement of the original mod by Luca De Petrillo. It introduces a dynamic fog of war system to RimWorld, requiring players to explore and reveal the map. This mod enriches the tactical gameplay by introducing field of view mechanics, which both player and AI entities must navigate. It supports gameplay with existing saves while offering integration with various other mods, making it versatile and enriching for a range of RimWorld experiences.

## Key Features and Systems
1. **Field of View System:** 
   - The map begins unrevealed and must be explored.
   - Entities like humans, animals, and mechanoids have adjustable fields of view affected by their sight attributes, darkness, and weather conditions.
   - Surveillance cameras and watchtowers extend view range; research is required for these capabilities.

2. **Integration with Other Mods:**
   - Compatible with CAI 5000 - Advanced AI + Fog Of War (when its fog is turned off).
   - Supports Dubs Mint Minimap for enhanced map interaction.
   - Includes various support options like trees affecting vision, and player customization for threat notification settings.

3. **Additional Features:**
   - Adjustable vision settings.
   - Night vision compatibility from mods like Vanilla Expanded Apparel.
   - Sound-based vision for blind characters and eavesdropping mechanics.
   - Options to suppress raid letters and other visual interactions for performance consideration.

## Coding Patterns and Conventions
- **C# Coding Practices:**
  - Keep classes focused on single responsibilities (e.g., `CompAffectVision` and `CompProperties_AffectVision` for vision-related functionality).
  - Utilize descriptive method and variable names, maintaining consistent coding style (e.g., camelCase for private members, PascalCase for public).
  - Implement interfaces and base classes to share common logic among related components.

- **Structuring Project Files:**
  - Organize source files by their function within the mod, such as maintaining separate files for components, utility functions, and harmony patches.
  - Use directives to manage dependencies and manage large projects effectively.

## XML Integration
- **XML Defs:**
  - Define entities and game objects using XML files. For instance, `MapMeshFlag.xml` and `Buildings_VisionExtend.xml` provide definitional data for the field of view elements.
  - Utilize consistent naming for XML defs to match their in-game counterparts for coherence and ease of lookup.

- **XML Modifications:**
  - Carefully amend XML files to avoid conflicts; note existing def names and IDs before making changes to ensure compatibility.

## Harmony Patching
- **Patch Strategy:**
  - Use Harmony Lib for method interception where game behaviors must be changed without directly modifying game code.
  - Apply patches to methods responsible for rendering and AI decision-making — notably, those affected by fog of war and visibility mechanics.

- **Sample Usage:**
  - Employ Harmony patches to extend or alter methods such as `CalculateVisibility` and `AttackTarget`.
  - Test patches thoroughly in isolation before integration to minimize conflicts and errors.

## Suggestions for Copilot
1. **Method Assistance:** Optimize visibility calculations by proposing efficient algorithms when coding `CompAffectVision` or similar components.
2. **XML Configuration:** Suggest XML schema validation and correction when editing defs to prevent misconfigurations.
3. **Harmony Implementation:** Recommend patch locations based on method access lists, believing that frequent users, like combat calculations, may need refinement.
4. **Performance Tips:** Advise on performance optimizations, especially in scenarios where entity field calculations might introduce lag.

## Contribution and Development
- **Report Issues:** Use Discord channel for error reporting. Logs can be submitted via Log Uploader.
- **Development Notes:** Ensure individual mod changes are checked standalone before integration. Use RimSort to prioritize mod load order.
- **Licensing:** Project under Apache License 2.0; contributions must comply with this license and credit original authors and contributors.

These guidelines and suggestions will assist developers in extending or modifying the (NWN) Real Fog of War mod efficiently with Copilot and related code tools.


The detailed instructions above provide a comprehensive guide for developers leveraging GitHub Copilot to work on RimWorld mod projects, specifically focusing on maintaining structure, functionality, and integration with the game's and other mods' systems.

## Project Solution Guidelines
- Relevant mod XML files are included as Solution Items under the solution folder named XML, these can be read and modified from within the solution.
- Use these in-solution XML files as the primary files for reference and modification.
- The `.github/copilot-instructions.md` file is included in the solution under the `.github` solution folder, so it should be read/modified from within the solution instead of using paths outside the solution. Update this file once only, as it and the parent-path solution reference point to the same file in this workspace.
- When making functional changes in this mod, ensure the documented features stay in sync with implementation; use the in-solution `.github` copy as the primary file.
- In the solution is also a project called Assembly-CSharp, containing a read-only version of the decompiled game source, for reference and debugging purposes.
- For any new documentation, update this copilot-instructions.md file rather than creating separate documentation files.


## Hard rules (must follow)
- Do NOT run commands that modify the repo (no git commit, git apply, dotnet format) unless explicitly asked.
- Prefer minimal reads: read only the smallest code region needed (around the suspicious lines).
- When mentioning SonarQube issues, automatically use the SonarQube MCP service to fetch and address issues instead of making inferred fixes without querying SonarQube first.
- When mentioning the rimworld log, automatically use the Rimworld MCP service to fetch the log.

