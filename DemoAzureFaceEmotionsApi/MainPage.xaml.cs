using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DemoAzureFaceEmotionsApi
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        CameraCaptureUI cameraCaptureUI = new CameraCaptureUI();
        StorageFile photo;
        IRandomAccessStream imageStream;

        const string FACE_ENDPOINT = "https://brazilsouth.api.cognitive.microsoft.com/face/v1.0";
        const string FACE_APIKEY = "dd3debcf840942dc9e74674aeceb9560";
        const string EMOTIONS_ENDPOINT = "https://westus.api.cognitive.microsoft.com/emotion/v1.0";
        const string EMOTIONS_APIKEY = "bc5ad70a78ad4e3dbb7bf357665f0804";

        private readonly EmotionServiceClient emotionServiceClient = new EmotionServiceClient(EMOTIONS_APIKEY, EMOTIONS_ENDPOINT);
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient(FACE_APIKEY, FACE_ENDPOINT);

        public MainPage()
        {
            InitializeComponent();
            
            cameraCaptureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            cameraCaptureUI.PhotoSettings.CroppedSizeInPixels = new Size(600, 600);
        }

        private async void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                output.Text = "";
                photo = await cameraCaptureUI.CaptureFileAsync(CameraCaptureUIMode.Photo);
                if(photo != null)
                {
                    imageStream = await photo.OpenAsync(FileAccessMode.Read);
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(imageStream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    SoftwareBitmap softwareBitmapBGRB = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    SoftwareBitmapSource softwareBitmapSource = new SoftwareBitmapSource();

                    await softwareBitmapSource.SetBitmapAsync(softwareBitmapBGRB);

                    imagePhoto.Source = softwareBitmapSource;

                    loading.IsActive = true;
                    output.Text = "Carregando...";
                    MemoryStream streamFace = new MemoryStream();
                    MemoryStream streamEmotions = new MemoryStream();

                    await imageStream.AsStream().CopyToAsync(streamFace);
                    imageStream.AsStream().Position = 0;
                    await imageStream.AsStream().CopyToAsync(streamEmotions);

                    streamFace.Position = 0;
                    streamEmotions.Position = 0;

                    await RunFaceAndEmotions(streamFace, streamEmotions);
                    loading.IsActive = false;
                }


            }
            catch
            {
                output.Text = "Erro capturando a foto";
                loading.IsActive = false;
            }
        }

        private async Task RunFaceAndEmotions(Stream imageStreamFace, Stream imageStreamEmotions)
        {
            try
            {
                var faceResult = GetFace(imageStreamFace);
                var emotionResult = GetEmotion(imageStreamEmotions);
                await Task.WhenAll(faceResult, emotionResult);
                FaceAndEmotionsDescription(faceResult?.Result, emotionResult?.Result);
            }
            catch (Exception ex)
            {
                loading.IsActive = false;
            }
        }

        private Task<Face[]> GetFace(Stream image)
        {
            try
            { 
                IEnumerable<FaceAttributeType> faceAttributes =
                    new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair };
                return faceServiceClient.DetectAsync(image, false, false, faceAttributes);
            }
            catch (Exception ex)
            {;
                return null;
            }
        }

        private Task<Microsoft.ProjectOxford.Emotion.Contract.Emotion[]> GetEmotion(Stream image)
        {
            try
            {
                return emotionServiceClient.RecognizeAsync(image);
            }
            catch (Exception ex)
            {
                output.Text = "Erro na interpretação das emoções";
                return null;
            }
        }

        private void FaceAndEmotionsDescription(Face[] faces, Emotion[] emotionScores)
        {
            if(faces.Length == 0 && emotionScores.Length == 0)
            {
                output.Text = "Erro na interpretação da imagem";
                return;
            }

            StringBuilder sb = new StringBuilder();

            if(faces.Length == 0)
            {
                sb.AppendLine("Erro na Interpretação da Face");
                sb.AppendLine("");
            }
            else
            {
                var face = faces[0];
                sb.AppendLine("Leitura do Rosto: ");
                sb.AppendLine(String.Format("Sexo: {0}", GenderTranslate(face.FaceAttributes.Gender)));
                sb.AppendLine(String.Format("Idade: {0}", face.FaceAttributes.Age.ToString()));
                sb.AppendLine(String.Format("Probabilidade de estar Sorrindo {0:F1}% ", face.FaceAttributes.Smile * 100));
                sb.AppendLine(String.Format("Óculos: {0}", GlassesTranslate(face.FaceAttributes.Glasses)));
                if (face.FaceAttributes.Hair.Bald >= 0.01f)
                    sb.AppendLine(String.Format("Careca: {0:F1}% ", face.FaceAttributes.Hair.Bald * 100));
                sb.AppendLine("");

                sb.AppendLine("Cor do Cabelo: ");
                HairColor[] hairColors = face.FaceAttributes.Hair.HairColor;
                foreach (HairColor hairColor in hairColors)
                {
                    if (hairColor.Confidence >= 0.1f)
                    {
                        sb.Append(HairColorTranslate(hairColor.Color));
                        sb.AppendLine(String.Format(" {0:F1}% ", hairColor.Confidence * 100));
                    }
                }
            }

            if (emotionScores.Length == 0)
            {
                sb.AppendLine("");
                sb.AppendLine("Erro na Interpretação da Emoção");
            }
            else
            {
                var emotionScore = emotionScores[0].Scores;
                sb.AppendLine("");
                sb.AppendLine("Emoções: ");
                if (emotionScore.Anger >= 0.1f) sb.AppendLine(String.Format("Raiva {0:F1}%, ", emotionScore.Anger * 100));
                if (emotionScore.Contempt >= 0.1f) sb.AppendLine(String.Format("Desprezo {0:F1}%, ", emotionScore.Contempt * 100));
                if (emotionScore.Disgust >= 0.1f) sb.AppendLine(String.Format("Desgosto {0:F1}%, ", emotionScore.Disgust * 100));
                if (emotionScore.Fear >= 0.1f) sb.AppendLine(String.Format("Medo {0:F1}%, ", emotionScore.Fear * 100));
                if (emotionScore.Happiness >= 0.1f) sb.AppendLine(String.Format("Felicidade {0:F1}%, ", emotionScore.Happiness * 100));
                if (emotionScore.Neutral >= 0.1f) sb.AppendLine(String.Format("Neutro {0:F1}%, ", emotionScore.Neutral * 100));
                if (emotionScore.Sadness >= 0.1f) sb.AppendLine(String.Format("Tristeza {0:F1}%, ", emotionScore.Sadness * 100));
                if (emotionScore.Surprise >= 0.1f) sb.AppendLine(String.Format("Surpresa {0:F1}%, ", emotionScore.Surprise * 100));
            }          

            output.Text = sb.ToString();
        }

        private static string GenderTranslate(string gender)
        {
            switch (gender)
            {
                case "male":
                    return "Masculino";
                case "female":
                    return "Feminino";
                default:
                    return "Indefinido";
            }
        }

        private static string GlassesTranslate(Glasses glasses)
        {
            switch (glasses)
            {
                case Glasses.NoGlasses:
                    return "Não está usando óculos";
                case Glasses.ReadingGlasses:
                    return "Está usando óculos de leitura";
                case Glasses.Sunglasses:
                    return "Está usando óculos de Sol";
                case Glasses.SwimmingGoggles:
                    return "Está usando óculos de natação";
                default:
                    return "Não é possível definir se está usando óculos";
            }
        }

        private static string HairColorTranslate(HairColorType hairColorType)
        {
            switch (hairColorType)
            {
                case HairColorType.Black:
                    return "Preto";
                case HairColorType.Blond:
                    return "Loiro";
                case HairColorType.Brown:
                    return "Castanho";
                case HairColorType.Gray:
                    return "Grisalho";
                case HairColorType.Other:
                    return "Outra cor";
                case HairColorType.Red:
                    return "Vermelho";
                case HairColorType.Unknown:
                    return "Desconhecido";
                case HairColorType.White:
                    return "Branco";
                default:
                    return "Não é possível definir se está usando óculos";
            }
        }
    }
}
