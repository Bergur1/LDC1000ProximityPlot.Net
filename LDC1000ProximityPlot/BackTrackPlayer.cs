using WMPLib;

namespace LDC1000ProximityPlot
{
    class BackTrackPlayer
    {
        private WindowsMediaPlayer wPlayer;

        public BackTrackPlayer()
        {
            wPlayer = new WindowsMediaPlayer();
        }

        public void SelectSong( int key )
        {
            //todo: derive base folder for music and select song based on frequency
            //wPlayer.URL = @"C:\Users\mainUser\Downloads\cry me a river.mp3";
        }

        public void Play()
        {
            wPlayer.controls.play();
        }
        public void Stop()
        {
            wPlayer.controls.stop();
        }
    }
}
