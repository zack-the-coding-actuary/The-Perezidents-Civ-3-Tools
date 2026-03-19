using System;

namespace Civ3Tools
{
    public class PlayerToggler
    {
        private const int BIQ_LENGTH_OFFSET = 38;
        private const int BIQ_SECTION_START = 562;
        private const int GAME_HEADER = 0x454D4147; // "GAME"
        // Offset of HumanPlayers within the GAME section (struct offset, includes the 8-byte section header).
        private const int HUMAN_PLAYERS_OFFSET_IN_GAME = 80;
        private const int REMAINING_PLAYERS_OFFSET_IN_GAME = 84; // immediately follows HumanPlayers in GAME struct

        // Returns a 32-element bool array representing the HumanPlayers bitmap from the GAME section.
        // Index i is true if player slot i is flagged as human.
        public static bool[] GetHumanPlayers(byte[] saveBytes)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            for (int i = scanStart; i <= saveBytes.Length - 4; i++)
            {
                if (BitConverter.ToInt32(saveBytes, i) != GAME_HEADER) continue;

                int offset = i + HUMAN_PLAYERS_OFFSET_IN_GAME;
                if (offset + 4 > saveBytes.Length)
                    throw new InvalidOperationException("Save file too short to read HumanPlayers.");

                int humanPlayers = BitConverter.ToInt32(saveBytes, offset);
                bool[] result = new bool[32];
                for (int slot = 0; slot < 32; slot++)
                    result[slot] = ((humanPlayers >> slot) & 1) == 1;
                return result;
            }

            throw new InvalidOperationException("GAME section not found in save file.");
        }

        // Returns a 32-element bool array representing the RemainingPlayers bitmap from the GAME section.
        // Index i is true if player slot i is still active (not eliminated).
        public static bool[] GetRemainingPlayers(byte[] saveBytes)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            for (int i = scanStart; i <= saveBytes.Length - 4; i++)
            {
                if (BitConverter.ToInt32(saveBytes, i) != GAME_HEADER) continue;

                int offset = i + REMAINING_PLAYERS_OFFSET_IN_GAME;
                if (offset + 4 > saveBytes.Length)
                    throw new InvalidOperationException("Save file too short to read RemainingPlayers.");

                int remainingPlayers = BitConverter.ToInt32(saveBytes, offset);
                bool[] result = new bool[32];
                for (int slot = 0; slot < 32; slot++)
                    result[slot] = ((remainingPlayers >> slot) & 1) == 1;
                return result;
            }

            throw new InvalidOperationException("GAME section not found in save file.");
        }

        // Sets or clears the given player slot's bit in the GAME section's HumanPlayers bitmap.
        // playerSlot is 0-based (0 = barbarians, 1..N = human/AI players).
        // Mutates saveBytes in place.
        public static void SetHumanPlayer(byte[] saveBytes, int playerSlot, bool isHuman)
        {
            int biqLength = BitConverter.ToInt32(saveBytes, BIQ_LENGTH_OFFSET);
            int scanStart = BIQ_SECTION_START + biqLength;

            for (int i = scanStart; i <= saveBytes.Length - 4; i++)
            {
                if (BitConverter.ToInt32(saveBytes, i) != GAME_HEADER) continue;

                int offset = i + HUMAN_PLAYERS_OFFSET_IN_GAME;
                if (offset + 4 > saveBytes.Length)
                    throw new InvalidOperationException("Save file too short to write HumanPlayers.");

                int humanPlayers = BitConverter.ToInt32(saveBytes, offset);
                int bit = 1 << playerSlot;
                humanPlayers = isHuman ? humanPlayers | bit : humanPlayers & ~bit;
                Array.Copy(BitConverter.GetBytes(humanPlayers), 0, saveBytes, offset, 4);
                return;
            }

            throw new InvalidOperationException("GAME section not found in save file.");
        }
    }
}
