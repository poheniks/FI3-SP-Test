# FI3-SP-Test
FI3 Concept Testing in Singleplayer
/*TODO:
     * Make an actual map to test the effectiveness of AI logic on a non-flat map
     *  -I expect ranged cav to need a new behavior under its BehaviorComponent. Circular skirmish will not be possible in tight locations. Perhaps raycast from each flag on init() to determine if a circular skirmish is viable?
     * Write logic for melee cav formation to steal empty points. Since they are a singular formation & not tied to the main team-level formation distribution logic, this should be a simple logic check 
     * 
     * Split this into multiple .cs
     * Rename this from Mod_Tools_Test to something more appropriate
     * 

/*BUGS:
     * Ranged cav formations will inherit foot soldiers on occasion.
     * 
     * Null reference crash on ManageFormations() called in my class TacticCapturePoint: Speculate that this happens when Formation.QuerySystem returns false on all isInfantry, isRanged, isCav, isCavRanged. 
     * 
     * Probably more that I don't know about yet
     */
/*IMPROVEMENTS:
     * (?) Add particle emitters to show who is capturing a point when the point is neutral
     * 
     */
/*GAMEPLAY NOTES:
     * Current AI system is limited to attacking or defending a total 6 points. Do not recommend using more than 4-5 active points. Tested up to 6 active points without issue, but leaves many points empty & AI are not smart enough to capitalize on that
     *  -AI will assign a ranged & inf formation below 3 active points. Past 3 active points, AI will alternate infantry and ranged formations between points
     *  -Hardcoded(?) formation limit is 10. Where 2 formations are designated as special, presumably for assigning to siege weapons/usable objects. Where the remaining 8 formations are used for infantry, ranged, & cav.
     *  -A possible workaround to the 10 (realistically 8) formation limit is to create multiple enemy teams
     * 
     * Maps will need to prevent snow-balling by careful placement of points & points w/ spawners 
     * 
     * Formations can get confused when assaulting a flag & the flag status changes. This can lead to losing an easily-won point if the formations & surround formations kept committing to that point
     * 
     * Ranged cav will sometimes charge directly onto points
     * Ranged cav are not fast in getting into their circular-pattern skirmish from a standstill. They will sit in place and shoot (which is okay) until it is their turn to join the column formation. 
     * Melee cav suck - no surprise here. Their current logic is primarily charging hit-and-runs. They will attempt to sit on a point if they can easily cap it (no enemies present)
     * 
     */
/*OTHER NOTES:
     * Adding more than one lane may significantly complicate AI logic. Multi-lane maps may need to simplify so there's only ever one active capture point
     * Have troop spawners upgrade over the duration of the map.
     * Siege weapons? They could easily designate choke points throughout the map
     * Have destructible buildings instead of flag points as the objective?
     * Have transportation objectives instead of flag points as the objective? Similar to TF2's payload gamemode. Attackers can push a battering ram or cart of explosives to destroy an objective
     * 
     */
