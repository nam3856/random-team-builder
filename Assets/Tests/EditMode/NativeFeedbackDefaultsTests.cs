using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RandomGame.Tests.EditMode
{
    public sealed class NativeFeedbackDefaultsTests
    {
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawnedObject in spawnedObjects)
            {
                if (spawnedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(spawnedObject);
                }
            }

            spawnedObjects.Clear();
        }

        [Test]
        public void NativeTmpTextReveal_HasAgreedDefaultsAndPlayEntryPoint()
        {
            Component feedback = CreateComponent("NativeTmpTextReveal");
            SerializedObject serializedFeedback = new SerializedObject(feedback);

            FindProperty(serializedFeedback, "targetText");
            AssertFloat(serializedFeedback, "secondsPerCharacter", 0.02f);
            AssertFloat(serializedFeedback, "maximumDuration", 1f);
            Assert.That(FindProperty(serializedFeedback, "playOnStart").boolValue, Is.True);
            AssertPublicPlayMethod(feedback);
        }

        [Test]
        public void NativeLightPulse_HasAgreedDefaultsAndPlayEntryPoint()
        {
            Component feedback = CreateComponent("NativeLightPulse");
            SerializedObject serializedFeedback = new SerializedObject(feedback);

            FindProperty(serializedFeedback, "targetLight");
            AssertFloat(serializedFeedback, "duration", 1.2f);
            AssertFloat(serializedFeedback, "peakIntensity", 2f);
            AssertPublicPlayMethod(feedback);
        }

        [Test]
        public void NativeImageFade_HasAgreedDefaultsAndPlayEntryPoint()
        {
            Component feedback = CreateComponent("NativeImageFade");
            SerializedObject serializedFeedback = new SerializedObject(feedback);

            FindProperty(serializedFeedback, "targetImage");
            AssertFloat(serializedFeedback, "duration", 0.2f);
            AssertFloat(serializedFeedback, "startAlpha", 0f);
            AssertFloat(serializedFeedback, "endAlpha", 1f);
            Assert.That(FindProperty(serializedFeedback, "playOnStart").boolValue, Is.True);
            AssertPublicPlayMethod(feedback);
        }

        [Test]
        public void NativeCompletionTextFeedback_HasAgreedDefaultsAndPlayEntryPoint()
        {
            Component feedback = CreateComponent("NativeCompletionTextFeedback");
            SerializedObject serializedFeedback = new SerializedObject(feedback);

            FindProperty(serializedFeedback, "targetText");
            AssertFloat(serializedFeedback, "secondsPerCharacter", 0.02f);
            AssertFloat(serializedFeedback, "maximumRevealDuration", 1f);
            AssertFloat(serializedFeedback, "colorPulseDuration", 1f);
            Assert.That(FindProperty(serializedFeedback, "playOnStart").boolValue, Is.True);

            Color gold = FindProperty(serializedFeedback, "goldColor").colorValue;
            Color expectedGold = new Color32(0xFF, 0xEB, 0x04, 0xFF);
            Assert.That(gold.r, Is.EqualTo(expectedGold.r).Within(0.001f));
            Assert.That(gold.g, Is.EqualTo(expectedGold.g).Within(0.001f));
            Assert.That(gold.b, Is.EqualTo(expectedGold.b).Within(0.001f));
            Assert.That(gold.a, Is.EqualTo(expectedGold.a).Within(0.001f));
            AssertPublicPlayMethod(feedback);
        }

        private Component CreateComponent(string typeName)
        {
            Type componentType = FindRequiredType(typeName);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(componentType), Is.True,
                typeName + " must be a MonoBehaviour.");

            GameObject gameObject = new GameObject(typeName + " Defaults Test");
            spawnedObjects.Add(gameObject);
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

            Assert.Fail("Required runtime component was not found: " + typeName);
            return null;
        }

        private static void AssertFloat(SerializedObject serializedObject, string fieldName, float expected)
        {
            Assert.That(FindProperty(serializedObject, fieldName).floatValue,
                Is.EqualTo(expected).Within(0.0001f), fieldName);
        }

        private static SerializedProperty FindProperty(SerializedObject serializedObject, string fieldName)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null,
                serializedObject.targetObject.GetType().Name + " must serialize " + fieldName + ".");
            return property;
        }

        private static void AssertPublicPlayMethod(Component feedback)
        {
            MethodInfo playMethod = feedback.GetType().GetMethod(
                "Play",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);

            Assert.That(playMethod, Is.Not.Null,
                feedback.GetType().Name + " must expose public void Play().");
            Assert.That(playMethod.ReturnType, Is.EqualTo(typeof(void)));
        }
    }
}
