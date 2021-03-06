using System;
using System.Collections.Generic;
using System.Text;

namespace Guardian_Roguelike.States
{
    class GameOverMenuState : StateBase
    {
        private string Filename;

        public override void EnterState()
        {
            Utilities.DeathData Data = (Utilities.DeathData)Utilities.InterStateResources.Instance.Resources["Game_DeathData"];
            Utilities.NotableEventsLog NEL = (Utilities.NotableEventsLog)Utilities.InterStateResources.Instance.Resources["Game_NotableEventsLog"];

            Filename = DateTime.Now.Date.ToString() + " - " + DateTime.Now.TimeOfDay.ToString() + ".txt";
            Filename = Filename.Replace('/', '-').Replace(':', '-');
            Filename = System.Windows.Forms.Application.ExecutablePath.Substring(0, System.Windows.Forms.Application.ExecutablePath.LastIndexOf('\\')+1) + "morgues\\" + Filename;
            System.IO.StreamWriter SW = new System.IO.StreamWriter(Filename);

            SW.WriteLine(Data.Player.FirstName + " " + Data.Player.LastName + ", the level 0 Dwarf");
            SW.WriteLine("He survived for " + Data.TurnsSurvived + " turns and descended " + Data.LevelsDescended + " levels into the pit.");
            SW.WriteLine("He was killed by " + Data.Killer.FirstName + " " + Data.Killer.LastName + " the " + Data.Killer.Type.ToString().Replace('_',' '));
            SW.WriteLine("Notable Events:");
            foreach (Utilities.NotableEvent NE in Utilities.NotableEventsLog.NotableEvents)
            {
                SW.WriteLine(NE.Turn.ToString() + ": " + NE.Description);
            }

            SW.Close();
        }

        public override void MainLoop()
        {
            while (true)
            {
                Render();

                if(ProcessInput(libtcodWrapper.Keyboard.WaitForKeyPress(true)))
                {
                    break;
                }
            }
        }

        public void Render()
        {
            Root.Clear();
            Root.PrintLine("You have died. Read all about your death in ", 10, 10, libtcodWrapper.LineAlignment.Left);
            Root.PrintLine(Filename, 10, 11, libtcodWrapper.LineAlignment.Left);
            Root.PrintLine("Tell your friends! Press Enter or Escape to continue.", 10,12, libtcodWrapper.LineAlignment.Left);
            Root.Flush();
        }

        public bool ProcessInput(libtcodWrapper.KeyPress KP)
        {
            if (KP.KeyCode == libtcodWrapper.KeyCode.TCODK_ENTER || KP.KeyCode == libtcodWrapper.KeyCode.TCODK_ESCAPE)
            {
                StateManager.PersistentStates["GameState"] = new GameState();
                StateManager.QueueState(StateManager.PersistentStates["MainMenuState"]);
                return true;
            }

            return false;
        }

        public override void ExitState()
        {

        }
    }
}
