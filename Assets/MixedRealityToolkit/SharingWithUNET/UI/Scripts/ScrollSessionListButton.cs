﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using MixedRealityToolkit.InputModule.EventData;
using MixedRealityToolkit.InputModule.InputHandlers;
using UnityEngine;

namespace MixedRealityToolkit.SharingWithUNET
{
    /// <summary>
    /// Attached to the 'up' and 'down' arrows in the scroll control
    /// </summary>
    public class ScrollSessionListButton : MonoBehaviour, IPointerHandler
    {
        /// <summary>
        /// Whether we are scrolling up (-1) in the list or down (1) in the list
        /// </summary>
        public int Direction;

        public void OnPointerUp(ClickEventData eventData) { }

        public void OnPointerDown(ClickEventData eventData) { }

        /// <summary>
        /// Called when the user clicks the control
        /// </summary>
        /// <param name="eventData">information about the click</param>
        public void OnPointerClicked(ClickEventData eventData)
        {
            ScrollingSessionListUIController.Instance.ScrollSessions(Direction);
            eventData.Use();
        }
    }
}