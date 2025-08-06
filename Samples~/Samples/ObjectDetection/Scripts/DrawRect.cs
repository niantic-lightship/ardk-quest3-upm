// Copyright 2022-2024 Niantic.

using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class DrawRect : MonoBehaviour
    {
        [SerializeField]
        private GameObject _rectanglePrefab;

        private List<UIRectObject> _rectangleObjects = new List<UIRectObject>();
        private List<int> _openIndices = new List<int>();

        private void Awake()
        {
            // DebugScript();
        }

        private void DebugScript()
        {
            var rect = new Rect(0, 0, 500, 100);
            CreateRect(rect, Color.red, "Red");

            rect = new Rect(0, 0, 100, 500);
            CreateRect(rect, Color.blue, "Blue");

            rect = new Rect(Screen.width / 2.0f, Screen.height / 2.0f, 500, 500);
            CreateRect(rect, Color.green, "Green");
        }

        public void CreateRect(Rect rect, Color color, string text)
        {
            if (_openIndices.Count == 0)
            {
                var newRect = Instantiate(_rectanglePrefab, parent: this.transform).GetComponent<UIRectObject>();

                _rectangleObjects.Add(newRect);
                _openIndices.Add(_rectangleObjects.Count - 1);
            }

            // Treat the first index as a queue
            int index = _openIndices[0];
            _openIndices.RemoveAt(0);

            UIRectObject rectangle = _rectangleObjects[index];
            rectangle.SetRectTransform(rect);
            rectangle.SetColor(color);
            rectangle.SetText(text);
            rectangle.gameObject.SetActive(true);
        }

        public void ClearRects()
        {
            for (var i = 0; i < _rectangleObjects.Count; i++)
            {
                _rectangleObjects[i].gameObject.SetActive(false);
                _openIndices.Add(i);
            }
        }
    }
}

