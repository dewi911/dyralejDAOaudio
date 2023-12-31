using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using WPFSoundVisualizationLib;
using System.ComponentModel;
using System.Windows.Forms;
using NAudio.Lame;




namespace dyralejDAOaudio
{
    public partial class MainWindow : Window
    {
        private System.Windows.Media.MediaPlayer mediaPlayer = new System.Windows.Media.MediaPlayer();
        private DispatcherTimer dispatcherTimer;
        private System.Windows.Threading.DispatcherTimer sliderUpdateTimer;
        private bool isDragging;
        private bool isEchoEnabled = false;
        
        private BlockAlignReductionStream stream = null;
        private DirectSoundOut output = null;
        private AudioFileReader audioFile;
        private WaveOutEvent outputDevice;
        private WaveOutEvent outputDevicebuffer;
        private WaveChannel32 channelStream;
        private bool isButtonPressed = false;
        private bool isHidden = true;
        private bool isSecondWindowVisible = false;
        private Window SecondWindow;
        private bool isExpanded = false;
        private AudioFileReader audioFileReader;
        private WaveStream waveStreamAdapter;
        private List<string> songPaths = new List<string>();
        private int currentSongIndex = 0;
        private bool repeatbut = false;








        public MainWindow()
        {
            InitializeComponent();
            LoadSongs();

            songsListBox.MouseDoubleClick += SongsListBox_MouseDoubleClick;
            NAudioEngine soundEngine = NAudioEngine.Instance;
            soundEngine.PropertyChanged += NAudioEngine_PropertyChanged;

            UIHelper.Bind(soundEngine, "CanStop", stopAudioRendering, System.Windows.Controls.Button.IsEnabledProperty);
            UIHelper.Bind(soundEngine, "CanPlay", playAudioRendering, System.Windows.Controls.Button.IsEnabledProperty);
            UIHelper.Bind(soundEngine, "CanPause", pauseAudioRendering, System.Windows.Controls.Button.IsEnabledProperty);
            UIHelper.Bind(soundEngine, "SelectionBegin", repeatStartTimeEdit, TimeEditor.ValueProperty, BindingMode.TwoWay);
            UIHelper.Bind(soundEngine, "SelectionEnd", repeatStopTimeEdit, TimeEditor.ValueProperty, BindingMode.TwoWay);


            spectrumAnalyzer.RegisterSoundPlayer(soundEngine);
            waveformTimeline.RegisterSoundPlayer(soundEngine);
        }


        #region NAudio Engine Events
        private void NAudioEngine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NAudioEngine engine = NAudioEngine.Instance;
            switch (e.PropertyName)
            {
                case "FileTag":
                    if (engine.FileTag != null)
                    {
                        TagLib.Tag tag = engine.FileTag.Tag;
                        if (tag.Pictures.Length > 0)
                        {
                            using (MemoryStream albumArtworkMemStream = new MemoryStream(tag.Pictures[0].Data.Data))
                            {
                                try
                                {
                                    BitmapImage albumImage = new BitmapImage();
                                    albumImage.BeginInit();
                                    albumImage.CacheOption = BitmapCacheOption.OnLoad;
                                    albumImage.StreamSource = albumArtworkMemStream;
                                    albumImage.EndInit();
                                    albumArtPanel.AlbumArtImage = albumImage;
                                }
                                catch (NotSupportedException)
                                {
                                    albumArtPanel.AlbumArtImage = null;
                                    
                                }
                                albumArtworkMemStream.Close();
                            }
                        }
                        else
                        {
                            albumArtPanel.AlbumArtImage = null;
                        }
                    }
                    else
                    {
                        albumArtPanel.AlbumArtImage = null;
                    }
                    break;
                case "ChannelPosition":
                    clockDisplay.Time = TimeSpan.FromSeconds(engine.ChannelPosition);
                    break;
                default:
                    
                    break;
            }

        }
        #endregion


        private void SongSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging && audioFile != null)
            { 
                audioFile.CurrentTime = TimeSpan.FromSeconds(songSlider.Value);
            }
        }

        private void SongSlider_DragStarted(object sender, RoutedEventArgs e)
        {
            isDragging = true;
        }

        private void SongSlider_DragCompleted(object sender, RoutedEventArgs e)
        {
            isDragging = false;

            if (audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(songSlider.Value);
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (audioFile != null)
            {
                audioFile.CurrentTime += TimeSpan.FromSeconds(10);
            }
        }

        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (audioFile != null)
            {
                if (audioFile.CurrentTime > TimeSpan.FromSeconds(10))
                {
                    audioFile.CurrentTime -= TimeSpan.FromSeconds(10);
                }
                else if(audioFile.CurrentTime < TimeSpan.FromSeconds(10))
                {
                    audioFile.CurrentTime = TimeSpan.Zero;
                }
            }
        }

        private float GetVolumeLevel()
        {
            if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                int blockSize = 512; 
                float[] buffer = new float[blockSize];
                try
                {
                    audioFile.Read(buffer, 0, blockSize);

                    float maxVolume = 0;
                    for (int i = 0; i < blockSize; i++)
                    {
                        if (Math.Abs(buffer[i]) > maxVolume)
                        {
                            maxVolume = Math.Abs(buffer[i]);
                        }
                    }
                    return maxVolume;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка при чтении аудиофайла: " + ex.Message);
                }
            }

            return 0;
        }

        private void SongsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string selectedSong = songsListBox.SelectedItem as string;
            if (selectedSong != null)
            {
                int index = songsListBox.Items.IndexOf(selectedSong); 
                if (index >= 0 && index < songPaths.Count)
                {
                    currentSongIndex = index; 
                    PlayNextSong(); 
                }
            }
        }
        private void LoadSongs()
        {
            string songsDirectory = @"C:\Users\Admin\Downloads"; 

            if (Directory.Exists(songsDirectory))
            {
                string[] songs = Directory.GetFiles(songsDirectory, "*.*") 
                                    .Where(file => file.ToLower().EndsWith(".mp3") || file.ToLower().EndsWith(".wav")) 
                                    .ToArray();

                foreach (string songPath in songs)
                {
                    string songName = System.IO.Path.GetFileName(songPath);
                    songsListBox.Items.Add(songName);
                    songPaths.Add(songPath);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Каталог с песнями не найден.");
            }
        }
        private void PlayAudio_Click_sec(object sender, RoutedEventArgs e)
        {
            string selectedSong = songsListBox.SelectedItem as string;
            if (selectedSong != null)
            {
                int index = songsListBox.Items.IndexOf(selectedSong);
                try
                {
                    if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        outputDevicebuffer?.Stop();
                        outputDevicebuffer?.Dispose();
                        outputDevice.Stop();
                        outputDevice.Dispose(); 
                    }
                    string songPath = songPaths[index];
                    audioFile = new AudioFileReader(songPath);
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    
                    System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                    dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                    dispatcherTimer.Start();
                    songSlider.Maximum = audioFile.TotalTime.TotalSeconds;
                    sliderUpdateTimer = new System.Windows.Threading.DispatcherTimer();
                    sliderUpdateTimer.Interval = TimeSpan.FromMilliseconds(100); 
                    sliderUpdateTimer.Tick += SliderUpdateTimer_Tick;
                    sliderUpdateTimer.Start();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка воспроизведения песни: {ex.Message}");
                }
            }
        }

        private void PlayNextSong()
        {
            if (songPaths != null && songPaths.Count > 0 && currentSongIndex < songPaths.Count)
            {
                string songPath = songPaths[currentSongIndex];
                try
                {
                    if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        outputDevicebuffer?.Stop();
                        outputDevicebuffer?.Dispose();
                        outputDevice.Stop();
                        outputDevice.Dispose();
                    }
                    
                    audioFile = new AudioFileReader(songPath);
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(audioFile);
                    
                    outputDevice.Play();

                    System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                    dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                    dispatcherTimer.Start();
                    songSlider.Maximum = audioFile.TotalTime.TotalSeconds;
                    sliderUpdateTimer = new System.Windows.Threading.DispatcherTimer();
                    sliderUpdateTimer.Interval = TimeSpan.FromMilliseconds(300);
                    sliderUpdateTimer.Tick += SliderUpdateTimer_Tick;
                    sliderUpdateTimer.Start();
                    UpdateCurrentlyPlayingInfo(songPath);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка воспроизведения песни: {ex.Message}");
                }
            }
        }


        private void UpdateCurrentlyPlayingInfo(string songPath)
        {
            string songInfo = $" {System.IO.Path.GetFileName(songPath)}"; 
            currentlyPlayingLabel.Content = songInfo; 
        }


        private void SliderUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (audioFile != null && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                songSlider.Value = audioFile.CurrentTime.TotalSeconds;
                if (repeatbut)
                {
                    if (audioFile.CurrentTime.TotalSeconds >= audioFile.TotalTime.TotalSeconds - 2)
                    {
                        if (outputDevice != null)
                        {
                            outputDevice.Stop();
                            outputDevice.Dispose();
                        }

                        if (audioFile != null)
                        {
                            audioFile.Dispose();
                        }
                        if (currentSongIndex < songPaths.Count)
                        {
                            PlayNextSong();
                        }
                    }
                       
                }
                else if(repeatbut == false)
                {
                    if (audioFile.CurrentTime.TotalSeconds >= audioFile.TotalTime.TotalSeconds - 2)
                    {

                        if (outputDevice != null)
                        {
                            outputDevice.Stop();
                            outputDevice.Dispose();
                        }

                        if (audioFile != null)
                        {
                            audioFile.Dispose();
                        }
                        currentSongIndex++;
                        if (currentSongIndex < songPaths.Count)
                        {
                            PlayNextSong();
                        }
                    }
                }
                
            }
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing && audioFile != null)
            {
                TimeSpan currentTime = audioFile.CurrentTime;
                TimeSpan duration = audioFile.TotalTime;
                currentTimeLabel.Content = $"{currentTime:mm\\:ss} / ";
                durationLabel.Content = $"{duration:mm\\:ss}";
            }
        }

        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            string selectedSong = songsListBox.SelectedItem as string;
            if (selectedSong != null)
            {
                string songPath = System.IO.Path.Combine(@"C:\Users\Admin\Downloads", selectedSong + ".mp3");
                try
                {
                    if (System.IO.File.Exists(songPath)) 
                    {
                        audioFile = new AudioFileReader(songPath);
                        WaveOutEvent outputDevice = new WaveOutEvent();
                        outputDevice.Init(audioFile);
                        outputDevice.Play();

                        System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                        dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
                        dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                        dispatcherTimer.Start();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Файл не найден: " + songPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка воспроизведения песни: {ex.Message}");
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Пожалуйста, выберите песню из списка.");
            }
        }



        private void PauseAudio_Click(object sender, RoutedEventArgs e)
        {
            if (audioFile != null && outputDevice.PlaybackState == PlaybackState.Paused)
            {
                outputDevice.Play();
            }
            else if (audioFile != null && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
            }
        }


        private void StopAudio_Click(object sender, RoutedEventArgs e)
        {
            if (audioFile != null)
            {
                outputDevice.Dispose();
                outputDevice = null;
                audioFile.Dispose();
                audioFile = null;
            }
        }
        private void repeatButton_Click(object sender, RoutedEventArgs e)
        {
            if(repeatbut == false)
            {
                System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml("#C561D3");
                System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                repeatAudioButton.Background = new SolidColorBrush(mediaColor);
                repeatbut = true;
            }
            else if (repeatbut)
            {
                System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml("#60016D");
                System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                repeatAudioButton.Background = new SolidColorBrush(mediaColor);
                repeatbut = false;
            }
            
        }


        private void ApplyEchoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (audioFile != null && outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    if (!isEchoEnabled)
                    {
                        EffectStream echoEffectStream;
                        channelStream = new WaveChannel32(audioFile);
                        echoEffectStream = new EffectStream(channelStream);
                        stream = new BlockAlignReductionStream(echoEffectStream);
                        echoEffectStream.ApplyEffects = true;
                        for (int i = 0; i < channelStream.WaveFormat.Channels; i++)
                        {
                            echoEffectStream.Effects.Add(new Echo());
                        }
                        outputDevice.Stop();
                        outputDevice.Dispose();
                        outputDevicebuffer?.Stop();
                        outputDevicebuffer?.Dispose();

                        outputDevice = new WaveOutEvent();
                        outputDevice.Init(stream);

                        outputDevice.Play();
                        isEchoEnabled = true;
                        System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml("#C561D3");
                        System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                        echoAudioButton.Background = new SolidColorBrush(mediaColor);
                    }
                    else
                    {
                        try
                        {
                            if (audioFile != null && outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                EffectStream echoEffectStream;
                                channelStream = new WaveChannel32(audioFile);
                                echoEffectStream = new EffectStream(channelStream);
                                stream = new BlockAlignReductionStream(echoEffectStream);
                                echoEffectStream.ApplyEffects = true;
                                for (int i = 0; i < channelStream.WaveFormat.Channels; i++)
                                {
                                    echoEffectStream.Effects.Add(new Echo());
                                }
                                    outputDevice.Stop();
                                    outputDevice.Dispose();

                                    outputDevice = new WaveOutEvent();
                                    outputDevice.Init(audioFile);
                                    outputDevice.Play();
                                isEchoEnabled = false;
                                System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml("#60016D");
                                System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                                echoAudioButton.Background = new SolidColorBrush(mediaColor);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Ошибка воспроизведения песни: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                    System.Windows.MessageBox.Show($"Ошибка воспроизведения песни: {ex.Message}");
            }
            
        }

        private void StopAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (outputDevice != null)
            {
                outputDevicebuffer?.Stop();
                outputDevicebuffer?.Dispose();
                outputDevice.Stop();
                outputDevice.Dispose();
                
            }
        }


        private void ExpandCollapseWindow()
        {
            DoubleAnimation widthAnimation = new DoubleAnimation();

            if (!isExpanded)
            {
                widthAnimation.From = 420; 
                widthAnimation.To = 1205; 
                isExpanded = true;
            }
            else
            {
                widthAnimation.From = 1205; 
                widthAnimation.To = 420; 
                isExpanded = false;
            }
            widthAnimation.Duration = TimeSpan.FromSeconds(0.3); 
            BeginAnimation(Window.WidthProperty, widthAnimation);
        }
        private void renderingopenButton_Click(object sender, RoutedEventArgs e)
        {

            ExpandCollapseWindow();
        }



        private void AddNewSong()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();

            openFileDialog1.Filter = "Audio Files|*.mp3;*.wav";
            openFileDialog1.Title = "Выберите аудио файл";

            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedSong = openFileDialog1.FileName;

                
                string songsDirectory = @"C:\Users\Admin\Downloads";

                if (Directory.Exists(songsDirectory))
                {
                    
                    if (selectedSong.ToLower().EndsWith(".mp3") || selectedSong.ToLower().EndsWith(".wav"))
                    {
                        
                        string newSongPath = System.IO.Path.Combine(songsDirectory, System.IO.Path.GetFileName(selectedSong));
                        if (System.IO.File.Exists(newSongPath))
                        {
                            System.Windows.MessageBox.Show("Этот файл уже в каталоге.");
                        }
                        else
                        {
                            System.IO.File.Copy(selectedSong, newSongPath);

                            string songName = System.IO.Path.GetFileName(newSongPath);
                            songsListBox.Items.Add(songName);
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Выбранный файл не является аудио файлом (mp3 или wav).");
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Каталог с песнями не найден.");
                }
            }
        }

        private void addSongButton_Click(object sender, EventArgs e)
        {
            AddNewSong();
        }
        private void convertButton_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "MP3 Files (*.mp3)|*.mp3";
            openFileDialog.Title = "Выберите MP3 файл для конвертации в WAV";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string inputFilePath = openFileDialog.FileName;
                string outputFilePath = "output2.wav"; 

                try
                {
                    using (var reader = new MediaFoundationReader(inputFilePath))
                    {
                        WaveFileWriter.CreateWaveFile(outputFilePath, reader);
                    }

                    System.Windows.MessageBox.Show("Конвертация завершена. Файл сохранен как output.wav");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Ошибка конвертации: " + ex.Message);
                }
            }
        }

        private void convertmp4Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();

            openFileDialog1.Filter = "Audio Files|*.mp3;*.wav";
            openFileDialog1.Title = "Выберите аудио файл";
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string inputpath = openFileDialog1.FileName;

            }

        }

        private void convertmp4_Click(object sender, RoutedEventArgs e)
        {


            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "MP4 Files (*.mp4)|*.mp4";
            openFileDialog.Title = "Выберите MP4 файл для конвертации в MP3";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string inputFilePath = openFileDialog.FileName;
                string outputFolder = System.IO.Path.GetDirectoryName(inputFilePath);
                string outputFileName = System.IO.Path.GetFileNameWithoutExtension(inputFilePath) + ".mp3";
                string outputFilePath = System.IO.Path.Combine(outputFolder, outputFileName);

                try
                {
                    using (var reader = new MediaFoundationReader(inputFilePath))
                    {
                        MediaFoundationEncoder.EncodeToMp3(reader, outputFilePath);
                    }

                    System.Windows.MessageBox.Show("Конвертация завершена. Файл сохранен как " + outputFileName + " в папке " + outputFolder);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Ошибка конвертации: " + ex.Message);
                }
            }
        }

        

        private void SelectSongtabinfo_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "MP3 Files (*.mp3)|*.mp3";
            openFileDialog.Title = "Выберите MP3 файл";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;

                try
                {
                    
                    var tagLibFile = TagLib.File.Create(filePath);

                    string title = !string.IsNullOrEmpty(tagLibFile.Tag.Title) ? tagLibFile.Tag.Title : "Название не доступно";
                    string artist = !string.IsNullOrEmpty(tagLibFile.Tag.FirstPerformer) ? tagLibFile.Tag.FirstPerformer : "Исполнитель не доступен";
                    string album = !string.IsNullOrEmpty(tagLibFile.Tag.Album) ? tagLibFile.Tag.Album : "Альбом не доступен";
                    TimeSpan duration = tagLibFile.Properties.Duration;
                    DateTime creationTime = System.IO.File.GetCreationTime(filePath);
                    long fileSize = new System.IO.FileInfo(filePath).Length;

                    string info = $"Название: {title}\n" +
                                  $"Исполнитель: {artist}\n" +
                                  $"Альбом: {album}\n" +
                                  $"Длительность: {duration}\n" +
                                  $"Дата создания файла: {creationTime}\n" +
                                  $"Размер файла: {fileSize} байт";

                    InfoTextBlock.Text = info;

                    tagLibFile.Dispose();
                   
                   
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        

        private void browseRenderingaudio_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();

        }

        private void playRenderingaudio_Click(object sender, RoutedEventArgs e)
        {
            if (NAudioEngine.Instance.CanPlay)
                NAudioEngine.Instance.Play();
        }

        private void pauseRenderingaudio_Click(object sender, RoutedEventArgs e)
        {
            if (NAudioEngine.Instance.CanPause)
                NAudioEngine.Instance.Pause();
        }

        private void stopRenderingaudio_Click(object sender, RoutedEventArgs e)
        {
            if (NAudioEngine.Instance.CanStop)
                NAudioEngine.Instance.Stop();
        }
        private void browsesaveRenderingaudio_Click(object sender, RoutedEventArgs e)
        {
            saveAudioRendering.IsEnabled = true;

            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult folderDialogResult = folderBrowserDialog.ShowDialog();
                if (folderDialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    FileTextSAVE.Text = folderBrowserDialog.SelectedPath;

                }
            }


        }

        private void saveRenderingaudio_Click(object sender, RoutedEventArgs e)
        {

            
            string inputFilePath = FileText.Text;
            int startRendering = 0;
            int endRendering = 0;
            string input = repeatStartTimeEdit.Value.ToString();
            string input1 = repeatStopTimeEdit.Value.ToString();


            string[] parts = input.Split(':', '.');
            string[] parts1 = input1.Split(':', '.');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes) &&
                 int.TryParse(parts[2], out int seconds))
                {
                    startRendering = hours * 3600 + minutes * 60 + seconds;
                }
            }
            if (parts1.Length >= 3)
            {
                if (int.TryParse(parts1[0], out int hours) && int.TryParse(parts1[1], out int minutes) &&
                 int.TryParse(parts1[2], out int seconds))
                {
                    endRendering = hours * 3600 + minutes * 60 + seconds;
                }
            }
                    string outputFolder = FileTextSAVE.Text;

                    using (var saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                    {
                        saveFileDialog.Filter = "MP3 файлы|*.mp3";
                        saveFileDialog.InitialDirectory = outputFolder;

                        DialogResult saveDialogResult = saveFileDialog.ShowDialog();

                        if (saveDialogResult == System.Windows.Forms.DialogResult.OK)
                        {
                            string outputFilePath = saveFileDialog.FileName;

                           
                            TimeSpan startTime = TimeSpan.FromSeconds(startRendering); 
                            TimeSpan endTime = TimeSpan.FromSeconds(endRendering); 

                            using (var audioFile = new Mp3FileReader(inputFilePath))
                            {
                                using (var reader = WaveFormatConversionStream.CreatePcmStream(audioFile))
                                {
                                    using (var writer = new LameMP3FileWriter(outputFilePath, reader.WaveFormat, LAMEPreset.VBR_90))
                                    {
                                        reader.CurrentTime = startTime;

                                        byte[] buffer = new byte[reader.WaveFormat.AverageBytesPerSecond * 2];

                                        while (reader.CurrentTime < endTime)
                                        {
                                            int bytesRead = reader.Read(buffer, 0, buffer.Length);
                                            if (bytesRead > 0)
                                            {
                                                writer.Write(buffer, 0, bytesRead);
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            System.Windows.MessageBox.Show($"Файл успешно обрезан и сохранен в формате MP3.");

                        }

                    }
        }


        private void OpenFile()
        {
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Filter = "(*.mp3)|*.mp3";
            if (openDialog.ShowDialog() == true)
            {
                playAudioRendering.IsEnabled = true;
                pauseAudioRendering.IsEnabled = true;
                stopAudioRendering.IsEnabled = true;
                NAudioEngine.Instance.OpenFile(openDialog.FileName);
                FileText.Text = openDialog.FileName;

                var tagLibFile = TagLib.File.Create(openDialog.FileName);

                string title = !string.IsNullOrEmpty(tagLibFile.Tag.Title) ? tagLibFile.Tag.Title : "Название не доступно";
                string artist = !string.IsNullOrEmpty(tagLibFile.Tag.FirstPerformer) ? tagLibFile.Tag.FirstPerformer : "Исполнитель не доступен";
                string album = !string.IsNullOrEmpty(tagLibFile.Tag.Album) ? tagLibFile.Tag.Album : "Альбом не доступен";
                TimeSpan duration = tagLibFile.Properties.Duration;
                DateTime creationTime = System.IO.File.GetCreationTime(openDialog.FileName);
                long fileSize = new System.IO.FileInfo(openDialog.FileName).Length;

                string info = $"Название: {title}\n" +
                              $"Исполнитель: {artist}\n" +
                              $"Альбом: {album}\n" +
                              $"Длительность: {duration}\n" +
                              $"Дата создания файла: {creationTime}\n" +
                              $"Размер файла: {fileSize} байт";

                InfoTextBlock.Text = info;

                tagLibFile.Dispose();
            }
        }

        
    }
}

