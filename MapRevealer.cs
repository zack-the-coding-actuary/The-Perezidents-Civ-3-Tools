using QueryCiv3;

namespace Civ3Tools
{
    public static class MapRevealer
    {
        // The embedded BIQ section always starts at byte 562 in a SAV file.
        // Its length (stored as an int at offset 38) must be skipped to reach the SAV sections.
        private const int BIQ_SECTION_START = 562;

        // Each TILE struct in the SAV file is exactly 212 bytes (Pack=1, Sequential layout).
        // This is used to stride through the tile array and to validate that we've found the
        // real tile array start (by confirming two consecutive TILE headers 212 bytes apart).
        private const int TILE_SIZE = 212;

        // Byte offsets of Height and Width within the WRLD struct, measured from the
        // start of the struct (i.e. from the "WRLD" header bytes themselves).
        private const int WRLD_HEIGHT_OFFSET = 22;
        private const int WRLD_WIDTH_OFFSET = 42;

        // Byte offsets of the ExploredBy and VisibleTo fields within each TILE struct.
        // Both are IntBitmap (4-byte int) where each bit represents one civ.
        // ExploredBy: civ has seen this tile at any point (reveals terrain permanently).
        // VisibleTo:  civ currently has a unit or city with line-of-sight to this tile.
        private const int TILE_EXPLORED_BY_OFFSET = 84;
        private const int TILE_VISIBLE_TO_OFFSET = 88;

        // 4-byte section header values interpreted as little-endian ints.
        private const int WRLD_HEADER = 0x444C5257; // "WRLD"
        private const int TILE_HEADER = 0x454C4954; // "TILE"

        // Civ index of the human player. Barbarians occupy civ 0, so the human player
        // is civ 1. The IntBitmap bit for civ N is (1 << N), so the human player's bit
        // is (1 << 1) = 2, written to the file as little-endian bytes: 02 00 00 00.
        private const int HUMAN_PLAYER_BIT = 1 << 1;

        public static byte[]? RevealedMap(string savePath)
        {
            // Util.ReadFile handles BLAST decompression for compressed SAV files.
            byte[] originalMap = Util.ReadFile(savePath);

            // The SAV sections begin immediately after the embedded BIQ block.
            // The BIQ block length is stored as a little-endian int at offset 38.
            int savSectionsStart = BIQ_SECTION_START + BitConverter.ToInt32(originalMap, 38);

            // Scan forward from the start of SAV sections to find the WRLD header.
            // We need WRLD for the map dimensions (width and height) to know how many tiles exist.
            // Civ3File.PopulateSections is not used here because it can miss headers that are
            // immediately preceded by printable ASCII bytes in the surrounding binary data.
            int wrldStart = -1;
            for (int i = savSectionsStart; i <= originalMap.Length - 4; i++)
            {
                if (BitConverter.ToInt32(originalMap, i) == WRLD_HEADER)
                {
                    wrldStart = i;
                    break;
                }
            }

            // Scan for the start of the TILE array. To avoid false positives from GAME section
            // data that might happen to contain the bytes "TILE", we require two consecutive
            // TILE headers separated by exactly TILE_SIZE bytes — the signature of a real tile array.
            int tileArrayStart = -1;
            int searchFrom = wrldStart >= 0 ? wrldStart : savSectionsStart;
            for (int i = searchFrom; i <= originalMap.Length - TILE_SIZE - 4; i++)
            {
                if (BitConverter.ToInt32(originalMap, i) == TILE_HEADER &&
                    BitConverter.ToInt32(originalMap, i + TILE_SIZE) == TILE_HEADER)
                {
                    tileArrayStart = i;
                    break;
                }
            }

            if (wrldStart == -1 || tileArrayStart == -1)
                return null;

            // The tile array covers half the map grid cells (Civ3 uses a diamond grid where
            // only every other cell is a valid tile, giving Width * Height / 2 total tiles).
            int height = BitConverter.ToInt32(originalMap, wrldStart + WRLD_HEIGHT_OFFSET);
            int width = BitConverter.ToInt32(originalMap, wrldStart + WRLD_WIDTH_OFFSET);
            int tileCount = width * height / 2;

            byte[] revealedMap = (byte[])originalMap.Clone();
            for (int i = 0; i < tileCount; i++)
            {
                int tileStart = tileArrayStart + i * TILE_SIZE;

                // On the first tile only, set all 32 bits so every possible civ has it explored.
                // All other tiles only set the human player's bit.
                int mask = i == 0 ? -1 : HUMAN_PLAYER_BIT;

                // OR in the mask without disturbing any bits not covered by it.
                int exploredBy = BitConverter.ToInt32(revealedMap, tileStart + TILE_EXPLORED_BY_OFFSET);
                BitConverter.GetBytes(exploredBy | mask).CopyTo(revealedMap, tileStart + TILE_EXPLORED_BY_OFFSET);

                int visibleTo = BitConverter.ToInt32(revealedMap, tileStart + TILE_VISIBLE_TO_OFFSET);
                BitConverter.GetBytes(visibleTo | mask).CopyTo(revealedMap, tileStart + TILE_VISIBLE_TO_OFFSET);
            }

            return revealedMap;
        }
    }
}
