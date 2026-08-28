using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RandomGame.Tests.PlayMode
{
    public sealed class NativeFeedbackPlayModeTests
    {
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject spawnedObject in spawnedObjects)
            {
                if (spawnedObject != null)
                {
                    UnityEngine.Object.Destroy(spawnedObject);
                }
            }

            spawnedObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TmpTextReveal_RevealsEveryCharacter()
        {
            Component text = CreateUiComponent("TMPro.TextMeshProUGUI", "Reveal Text");
            SetPublicProperty(text, "text", "ABCDE");

            Component feedback = AddComponent(text.gameObject, "NativeTmpTextReveal");
            SetPrivateField(feedback, "targetText", text);
            SetPrivateField(feedback, "playOnStart", false);
            InvokePlay(feedback);

            Assert.That((int)GetPublicProperty(text, "maxVisibleCharacters"), Is.Zero);
            yield return new WaitForSecondsRealtime(0.15f);

            int visibleCharacters = (int)GetPublicProperty(text, "maxVisibleCharacters");
            Assert.That(visibleCharacters, Is.GreaterThanOrEqualTo(5));
        }

        [UnityTest]
        public IEnumerator LightPulse_ReachesPeakAndRestoresOriginalIntensity()
        {
            Component light = CreateComponent("UnityEngine.Rendering.Universal.Light2D", "Pulse Light");
            SetPublicProperty(light, "intensity", 0.65f);

            Component feedback = AddComponent(light.gameObject, "NativeLightPulse");
            SetPrivateField(feedback, "targetLight", light);
            SetPrivateField(feedback, "duration", 0.3f);
            SetPrivateField(feedback, "peakIntensity", 2f);
            InvokePlay(feedback);

            Assert.That((float)GetPublicProperty(light, "intensity"), Is.EqualTo(0f).Within(0.01f));
            float maximumIntensity = 0f;
            float samplingDeadline = Time.realtimeSinceStartup + 0.38f;
            while (Time.realtimeSinceStartup < samplingDeadline)
            {
                yield return null;
                maximumIntensity = Mathf.Max(
                    maximumIntensity,
                    (float)GetPublicProperty(light, "intensity"));
            }

            Assert.That(maximumIntensity, Is.GreaterThan(1.5f));
            float restoredIntensity = (float)GetPublicProperty(light, "intensity");
            Assert.That(restoredIntensity, Is.EqualTo(0.65f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator ImageFade_TransitionsFromStartToEndAlpha()
        {
            Component image = CreateUiComponent("UnityEngine.UI.Image", "Fade Image");
            Color originalColor = new Color(0.25f, 0.5f, 0.75f, 0.4f);
            SetPublicProperty(image, "color", originalColor);

            Component feedback = AddComponent(image.gameObject, "NativeImageFade");
            SetPrivateField(feedback, "targetImage", image);
            SetPrivateField(feedback, "duration", 0.05f);
            SetPrivateField(feedback, "startAlpha", 0f);
            SetPrivateField(feedback, "endAlpha", 1f);
            SetPrivateField(feedback, "playOnStart", false);
            InvokePlay(feedback);

            Color startedColor = (Color)GetPublicProperty(image, "color");
            Assert.That(startedColor.a, Is.EqualTo(0f).Within(0.05f));

            yield return new WaitForSecondsRealtime(0.09f);

            Color completedColor = (Color)GetPublicProperty(image, "color");
            Assert.That(completedColor.r, Is.EqualTo(originalColor.r).Within(0.01f));
            Assert.That(completedColor.g, Is.EqualTo(originalColor.g).Within(0.01f));
            Assert.That(completedColor.b, Is.EqualTo(originalColor.b).Within(0.01f));
            Assert.That(completedColor.a, Is.EqualTo(1f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator CompletionText_RevealsAndReturnsFromGoldPulse()
        {
            Component text = CreateUiComponent("TMPro.TextMeshProUGUI", "Completion Text");
            Color originalColor = new Color(0.2f, 0.35f, 0.8f, 1f);
            SetPublicProperty(text, "text", "DONE");
            SetPublicProperty(text, "color", originalColor);

            Component feedback = AddComponent(text.gameObject, "NativeCompletionTextFeedback");
            SetPrivateField(feedback, "targetText", text);
            SetPrivateField(feedback, "maximumRevealDuration", 0.05f);
            SetPrivateField(feedback, "colorPulseDuration", 0.6f);
            SetPrivateField(feedback, "playOnStart", false);
            InvokePlay(feedback);

            Assert.That((int)GetPublicProperty(text, "maxVisibleCharacters"), Is.Zero);
            float maximumRed = originalColor.r;
            float maximumGreen = originalColor.g;
            float minimumBlue = originalColor.b;
            float samplingDeadline = Time.realtimeSinceStartup + 0.45f;
            while (Time.realtimeSinceStartup < samplingDeadline)
            {
                yield return null;
                Color sampledColor = (Color)GetPublicProperty(text, "color");
                maximumRed = Mathf.Max(maximumRed, sampledColor.r);
                maximumGreen = Mathf.Max(maximumGreen, sampledColor.g);
                minimumBlue = Mathf.Min(minimumBlue, sampledColor.b);
            }

            Assert.That(maximumRed, Is.GreaterThan(originalColor.r + 0.3f));
            Assert.That(maximumGreen, Is.GreaterThan(originalColor.g + 0.3f));
            Assert.That(minimumBlue, Is.LessThan(originalColor.b - 0.3f));

            yield return new WaitForSecondsRealtime(0.25f);

            int visibleCharacters = (int)GetPublicProperty(text, "maxVisibleCharacters");
            Color completedColor = (Color)GetPublicProperty(text, "color");
            Assert.That(visibleCharacters, Is.GreaterThanOrEqualTo(4));
            Assert.That(completedColor.r, Is.EqualTo(originalColor.r).Within(0.05f));
            Assert.That(completedColor.g, Is.EqualTo(originalColor.g).Within(0.05f));
            Assert.That(completedColor.b, Is.EqualTo(originalColor.b).Within(0.05f));
            Assert.That(completedColor.a, Is.EqualTo(originalColor.a).Within(0.05f));
        }

        [Test]
        public void MusicPlayer_WithNoClip_ReturnsWithoutPlayingOrThrowing()
        {
            GameObject gameObject = CreateGameObject("Optional Music");
            AudioSource audioSource = gameObject.AddComponent<AudioSource>();
            Component musicPlayer = AddComponent(gameObject, "MusicPlayer");

            MethodInfo playMethod = musicPlayer.GetType().GetMethod(
                "Play",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(playMethod, Is.Not.Null);
            Assert.DoesNotThrow(() => playMethod.Invoke(musicPlayer, null));
            Assert.That(audioSource.isPlaying, Is.False);
        }

        private Component CreateComponent(string typeName, string objectName)
        {
            GameObject gameObject = CreateGameObject(objectName);
            return gameObject.AddComponent(FindRequiredType(typeName));
        }

        private Component CreateUiComponent(string typeName, string objectName)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            spawnedObjects.Add(gameObject);
            return gameObject.AddComponent(FindRequiredType(typeName));
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            spawnedObjects.Add(gameObject);
            return gameObject;
        }

        private static Component AddComponent(GameObject gameObject, string typeName)
        {
            Type componentType = FindRequiredType(typeName);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(componentType), Is.True,
                typeName + " must be a MonoBehaviour.");
            return gameObject.AddComponent(componentType);
        }

        private static Type FindRequiredType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type directMatch = assembly.GetType(typeName, false);
                if (directMatch != null)
                {
                    return directMatch;
                }

                try
                {
                    Type nameMatch = assembly.GetTypes().FirstOrDefault(type => type.Name == typeName);
                    if (nameMatch != null)
                    {
                        return nameMatch;
                    }
                }
                catch (ReflectionTypeLoadException exception)
                {
                    Type nameMatch = exception.Types.FirstOrDefault(type => type != null && type.Name == typeName);
                    if (nameMatch != null)
                    {
                        return nameMatch;
                    }
                }
            }

            Assert.Fail("Required component type was not found: " + typeName);
            return null;
        }

        private static void SetPrivateField(Component component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                component.GetType().Name + " must declare serialized field " + fieldName + ".");
            field.SetValue(component, value);
        }

        private static object GetPublicProperty(Component component, string propertyName)
        {
            PropertyInfo property = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                component.GetType().Name + " must expose property " + propertyName + ".");
            return property.GetValue(component, null);
        }

        private static void SetPublicProperty(Component component, string propertyName, object value)
        {
            PropertyInfo property = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                component.GetType().Name + " must expose property " + propertyName + ".");
            property.SetValue(component, value, null);
        }

        private static void InvokePlay(Component feedback)
        {
            MethodInfo playMethod = feedback.GetType().GetMethod(
                "Play",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(playMethod, Is.Not.Null, feedback.GetType().Name + " must expose public void Play().");
            playMethod.Invoke(feedback, null);
        }
    }
}
