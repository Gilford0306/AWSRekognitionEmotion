using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using MaterialSkin;
using Microsoft.Extensions.Configuration;

namespace AWSRekognitionEmotion
{
    public partial class Form1 : MaterialSkin.Controls.MaterialForm
    {
        private IConfiguration configuration;
        private string accessKey;
        private string secretKey;

        private readonly AmazonRekognitionClient _rekognitionClient;
        private PictureBox pictureBox1 = new PictureBox();
        private readonly MaterialSkinManager materialSkinManager;
        bool flag = false;
        private float panelRatioWidth = 0.8f; 
        private float panelRatioHeight = 0.7f;

        public Form1()
        {
            InitializeComponent();

            materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(Primary.BlueGrey800, Primary.BlueGrey900, Primary.BlueGrey500, Accent.LightBlue200, TextShade.WHITE);

            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            accessKey = configuration["AWS:AccessKey"];
            secretKey = configuration["AWS:SecretKey"];
            BasicAWSCredentials credentials = new BasicAWSCredentials(accessKey, secretKey);
            _rekognitionClient = new AmazonRekognitionClient(credentials, Amazon.RegionEndpoint.EUWest1);
            panel2.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;


        }

        private async Task<DetectFacesResponse> DetectEmotionsAsync(string imagePath)
        {
            using (var imageStream = new MemoryStream(File.ReadAllBytes(imagePath)))
            {
                var request = new DetectFacesRequest
                {
                    Image = new Amazon.Rekognition.Model.Image { Bytes = imageStream },
                    Attributes = new List <string> { "ALL" } 
                };

                return await _rekognitionClient.DetectFacesAsync(request);
            }
        }

        private void DisplayEmotions(DetectFacesResponse response)
        {
            if (response.FaceDetails.Count > 0)
            {
                var emotions = response.FaceDetails.SelectMany(faceDetail => faceDetail.Emotions)
                    .Where(emotion => emotion.Confidence > 1); 

                if (emotions.Any())
                {
                    var emotionText = string.Join("\n ", emotions.Select(emotion => $"{emotion.Type} ({emotion.Confidence:F0}%)"));
                    MessageBox.Show($"Emotion Detectedи:\n{emotionText}", "Emotion Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Emotions not detected with confidence over 0%.", "Emotion Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

            }
            else
            {
                MessageBox.Show("No face on Photo", "Emotion Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        private async void materialFlatButton1_Click(object sender, EventArgs e)
        {
            flag = true;
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Image files ( *.png)|*.png";
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string filePath = openFileDialog.FileName;
                        panel1.Controls.Remove(pictureBox1);
                        pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
                        pictureBox1.Image = System.Drawing.Image.FromFile(filePath);
                        pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
                        panel1.Controls.Add(pictureBox1);
                        pictureBox1.Image = System.Drawing.Image.FromFile(filePath);

                        var detectEmotionsResponse = await DetectEmotionsAsync(filePath);

                        DisplayEmotions(detectEmotionsResponse);

                        if (pictureBox1.Image != null && detectEmotionsResponse.FaceDetails.Count > 0)
                        {
                            var graphics = Graphics.FromImage(pictureBox1.Image);
                            var pen = new Pen(Color.Red, 3);

                            foreach (var faceDetail in detectEmotionsResponse.FaceDetails)
                            {
                                var boundingBox = faceDetail.BoundingBox;
                                var rect = new Rectangle(
                                    (int)(boundingBox.Left * pictureBox1.Width),
                                    (int)(boundingBox.Top * pictureBox1.Height),
                                    (int)(boundingBox.Width * pictureBox1.Width),
                                    (int)(boundingBox.Height * pictureBox1.Height));
                                graphics.DrawRectangle(pen, rect);
                            }

                            pictureBox1.Refresh();
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void materialFlatButton2_Click(object sender, EventArgs e)
        {
            try
            {
                flag = false;
                string imageUrl = textBox1.Text.Trim();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    panel1.Controls.Remove(pictureBox1);
                    pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
                    pictureBox1.Image = System.Drawing.Image.FromFile(await DownloadImageAsync(imageUrl)); ;
                    pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
                    panel1.Controls.Add(pictureBox1);
                    DownloadImageAsync(imageUrl);
                    var detectEmotionsResponse = await DetectEmotionsAsync(await DownloadImageAsync(imageUrl));
                    DisplayEmotions(detectEmotionsResponse);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private async Task<string> DownloadImageAsync(string url)
        {
            using (var client = new WebClient())
            {
                var data = await client.DownloadDataTaskAsync(url);
                string fileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                File.WriteAllBytes(fileName, data);
                return fileName;
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            SetPanelSize();
            panel1.Refresh();
        }
        private void SetPanelSize()
        {
            int newWidth = (int)(this.ClientSize.Width * panelRatioWidth);
            int newHeight = (int)(this.ClientSize.Height * panelRatioHeight);
            panel1.Size = new Size(newWidth, newHeight);
            panel2.Size = new Size(newWidth, this.panel2.Height);
        }


    }
}