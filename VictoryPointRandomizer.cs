using QueryCiv3;
using QueryCiv3.Sav;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Civ3Tools
{
    internal static class VictoryPointRandomizer
    {
        private const int GAME_HEADER = 0x454D4147; // "GAME"
        private const int TILE_HEADER = 0x454C4954; // "TILE"
        private const int WRLD_HEADER = 0x444C5257; // "WRLD"
        private const int VLOC_HEADER = 0x434F4C56; // "VLOC"
        private const int SAV_BIQ_LENGTH_OFFSET = 38;
        private const int SAV_BIQ_SECTION_START = 562;
        private const int SAV_VPL_OFFSET_IN_TILE = 38; // short at bytes [2..3] of UnknownBuffer3
        private const int BIQ_SECTION_HEADERS_START = 736;
        private const int WRLD_WIDTH_OFFSET = 42; // offset of Width field within WRLD section
        private const int VLOC_X_OFFSET = 12;     // offset of XCoordinate within a VLOC entry
        private const int VLOC_Y_OFFSET = 16;     // offset of YCoordinate within a VLOC entry
        private const int GAME_LEN_1 = 16; // Length + DefaultGameRules + DefaultVictoryConditions + NumberOfPlayableCivs
        private const int NUMBER_OF_PLAYABLE_CIVS_OFFSET = 12; // offset within LEN_1

        // Offsets within the LEN_2 block
        private const int VICTORY_LOCATIONS_BYTE_OFFSET = 1; // Flags[1], within Flags[4]
        private const byte VICTORY_LOCATIONS_BIT = 5;
        private const int AUTO_PLACE_VICTORY_LOCATIONS_OFFSET = 12; // Flags(4) + PlaceCaptureUnits(4) + AutoPlaceKings(4)

        private const int SAV_BASE_TERRAIN_OFFSET_IN_TILE = 57; // Flags2[5] & 0x0F in C3C block
        private const int WATER_TERRAIN_MIN = 11; // 11=coast, 12=sea, 13=ocean

        // Shuffles existing VLOC entries onto random valid land tiles in-place.
        // Assumes the save already has VLOC entries (e.g. from default game placement).
        public static unsafe void RandomizeVictoryPoints(byte[] savBytes)
        {
            var random = new Random();
            int biqLength = BitConverter.ToInt32(savBytes, SAV_BIQ_LENGTH_OFFSET);
            int savStart  = SAV_BIQ_SECTION_START + biqLength;
            int tileSize  = sizeof(TILE);

            fixed (byte* bytePtr = savBytes)
            {
                byte* end = bytePtr + savBytes.Length;

                // Pass 1: find map width from WRLD section
                int mapWidth = 0;
                byte* scan = bytePtr + savStart;
                while (scan + 4 <= end)
                {
                    if (*(int*)scan == WRLD_HEADER)
                    {
                        mapWidth = *(int*)(scan + WRLD_WIDTH_OFFSET);
                        break;
                    }
                    scan++;
                }
                if (mapWidth == 0) return;
                int halfWidth = mapWidth / 2;

                // Pass 2: scan all TILEs, collect land tiles with their coordinates
                var landTiles = new List<(int offset, int x, int y)>();
                var coordToTileOffset = new Dictionary<(int, int), int>();
                int tileIndex = 0;
                scan = bytePtr + savStart;
                while (scan + tileSize <= end)
                {
                    if (*(int*)scan == TILE_HEADER)
                    {
                        int tileY = tileIndex / halfWidth;
                        int tileX = (tileIndex % halfWidth) * 2 + (tileY % 2);
                        int offset = (int)(scan - bytePtr);
                        coordToTileOffset[(tileX, tileY)] = offset;
                        if (!IsWaterTile(scan))
                            landTiles.Add((offset, tileX, tileY));
                        tileIndex++;
                        scan += tileSize;
                    }
                    else scan++;
                }

                // Variable to store starting locations from VLOCs found
                var startingLocs = new List<(int x, int y)>();

                // Pass 3: find VLOC entries, clear old tile VP flags, collect VLOC offsets
                var vlocOffsets = new List<int>();
                scan = bytePtr + savStart;
                while (scan + sizeof(VLOC) <= end)
                {
                    if (*(int*)scan == VLOC_HEADER)
                    {
                        int oldX = *(int*)(scan + VLOC_X_OFFSET);
                        int oldY = *(int*)(scan + VLOC_Y_OFFSET);
                        if (coordToTileOffset.TryGetValue((oldX, oldY), out int tileOffset))
                            *(short*)(bytePtr + tileOffset + SAV_VPL_OFFSET_IN_TILE) = -1;
                        vlocOffsets.Add((int)(scan - bytePtr));
                        startingLocs.Add((oldX, oldY));
                        scan += sizeof(VLOC);
                    }
                    else scan++;
                }

                if (vlocOffsets.Count == 0) return;

                // Some infinite loop safety variables
                int attempts = 0;
                int minDistance = 12;

                // Loop until we find a valid set of VP distributions
                while (true)
                {
                    // Increment attempts/minDistance appropriately
                    attempts++;
                    if (attempts == 500)
                    {
                        minDistance--;
                        if (minDistance == 7) throw new InvalidOperationException("Error: Could not place all VP locations at least 8 tiles away from player spawns and each other");
                        attempts = 0;
                    }

                    // Fisher-Yates shuffle land tile list, then assign first N to existing VLOCs
                    for (int i = landTiles.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (landTiles[i], landTiles[j]) = (landTiles[j], landTiles[i]);
                    }

                    // Create list containing starting locations and new placements of VPs
                    var locs = new List<(int x, int y)>(startingLocs);
                    var VPlocs = new List<(int offset, int x, int y)>();

                    foreach (var landTile in landTiles)
                    {
                        var (tileOffset, newX, newY) = landTile;

                        // Skip this landTile if within 10 units of another spawn/VP location
                        if (locs.Any(loc => TileDistance(loc.x, loc.y, newX, newY, mapWidth) < minDistance)) continue;
                        else
                        {
                            locs.Add((newX, newY));
                            VPlocs.Add((tileOffset, newX, newY));
                        }

                        // If we have enough VP locs, early quit
                        if (VPlocs.Count == vlocOffsets.Count) break;
                    }

                    // If we couldn't complete our placement, try again
                    if (VPlocs.Count < vlocOffsets.Count) continue;

                    for (int i = 0; i < vlocOffsets.Count; i++)
                    {
                        var (tileOffset, newX, newY) = VPlocs[i];
                        int vlocOffset = vlocOffsets[i];
                        *(short*)(bytePtr + tileOffset + SAV_VPL_OFFSET_IN_TILE) = 0;
                        *(int*)(bytePtr + vlocOffset + VLOC_X_OFFSET) = newX;
                        *(int*)(bytePtr + vlocOffset + VLOC_Y_OFFSET) = newY;
                    }
                    break;
                }
            }
        }

        private static int CoordinatesToTileIndex(int x, int y, int mapWidth)
            => y * (mapWidth / 2) + x / 2;

        private static int TileDistance(int x1, int y1, int x2, int y2, int mapWidth)
        {
            int dx = Math.Abs(x1 - x2);
            dx = Math.Min(dx, mapWidth - dx); // account for horizontal wrap
            return (dx + Math.Abs(y1 - y2)) / 2;
        }

        private static unsafe bool IsWaterTile(byte* tilePtr)
            => (*(tilePtr + SAV_BASE_TERRAIN_OFFSET_IN_TILE) & 0x0F) >= WATER_TERRAIN_MIN;
        private static unsafe bool AssignVictoryPoint(byte[] savBytes, int tileIndex)
        {
            int biqLength = BitConverter.ToInt32(savBytes, SAV_BIQ_LENGTH_OFFSET);
            int savStart  = SAV_BIQ_SECTION_START + biqLength;
            int tileSize  = sizeof(TILE);
            int count     = 0;

            fixed (byte* bytePtr = savBytes)
            {
                byte* scan = bytePtr + savStart;
                byte* end  = bytePtr + savBytes.Length;

                while (scan + tileSize <= end)
                {
                    if (*(int*)scan == TILE_HEADER)
                    {
                        if (count == tileIndex)
                        {
                            *(short*)(scan + SAV_VPL_OFFSET_IN_TILE) = 0;
                            return true;
                        }
                        count++;
                        scan += tileSize;
                    }
                    else scan++;
                }
            }
            return false;
        }
    }
}
