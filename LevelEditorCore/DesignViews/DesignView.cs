﻿//Copyright © 2014 Sony Computer Entertainment America LLC. See License.txt.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;

using Sce.Atf;
using Sce.Atf.Adaptation;
using Sce.Atf.Controls;
using Sce.Atf.VectorMath;


using ControlSchemes = Sce.Atf.Rendering.ControlSchemes;
using MayaControlScheme = Sce.Atf.Rendering.MayaControlScheme;
using MayaLaptopControlScheme = Sce.Atf.Rendering.MayaLaptopControlScheme;
using MaxControlScheme = Sce.Atf.Rendering.MaxControlScheme;
using CameraController = Sce.Atf.Rendering.CameraController;


namespace LevelEditorCore
{
    /// <summary>
    /// Base designview class
    /// </summary>
    public abstract class DesignView : IDesignView, ISnapSettings
    {
        public DesignView()
        {            
            QuadView = new QuadPanelControl();
            CameraController.LockOrthographic = true;

            m_frequency = Stopwatch.Frequency;
            m_baseTicks = Stopwatch.GetTimestamp();
            m_lastTicks = m_baseTicks;
        }
        
        #region IDesignView Members

        public Control HostControl
        {
            get { return QuadView; }
        }

        public DesignViewControl ActiveView
        {
            get { return (DesignViewControl)QuadView.ActiveControl; }
        }

        public IEnumerable<DesignViewControl> Views
        {
            get 
            {
                foreach (Control ctrl in QuadView.Controls)
                {                    
                    DesignViewControl view = ctrl as DesignViewControl;
                    if (view != null && view.Width > 1 && view.Height > 1)
                    {
                        yield return view;                        
                    }
                }                                    
            }
        }

        private ViewModes m_viewMode = ViewModes.Quad;
        public ViewModes ViewMode
        {
            get { return m_viewMode; }
            set
            {
                m_viewMode = value;
                switch (m_viewMode)
                {
                    case ViewModes.Single:                        
                        QuadView.EnableX = false;
                        QuadView.EnableY = false;
                        QuadView.SplitterThickness = 0;
                        QuadView.SplitX = 1.0f;
                        QuadView.SplitY = 1.0f;                        
                        break;
                    case ViewModes.DualHorizontal:
                        if (QuadView.ActiveControl == QuadView.TopLeft
                            || QuadView.ActiveControl == QuadView.TopRight)
                            QuadView.SplitX = 1.0f;
                        else
                            QuadView.SplitX = 0.0f;
                        QuadView.SplitY = 0.5f;
                        QuadView.EnableX = false;
                        QuadView.EnableY = true;
                        QuadView.SplitterThickness = DefaultSplitterThickness;
                        
                        break;
                    case ViewModes.DualVertical:
                        
                        if (QuadView.ActiveControl == QuadView.TopLeft
                            || QuadView.ActiveControl == QuadView.BottomLeft)
                            QuadView.SplitY = 1.0f;
                            else
                            QuadView.SplitY = 0.0f;

                        QuadView.SplitterThickness = DefaultSplitterThickness;
                        QuadView.EnableX = true;
                        QuadView.EnableY = false;
                        QuadView.SplitX = 0.5f;
                        
                        break;
                    case ViewModes.Quad:
                        QuadView.EnableX = true;
                        QuadView.EnableY = true;
                        QuadView.SplitterThickness = DefaultSplitterThickness;
                        QuadView.SplitX = 0.5f;
                        QuadView.SplitY = 0.5f;
                        break;
                }

                QuadView.Refresh();
                
            }
        }
        public object Context
        {
            get { return m_context; }
            set
            {
                ContextChanging(this, EventArgs.Empty);

                if (m_validationContext != null)
                {
                    m_validationContext.Cancelled -= validationContext_Refresh;
                    m_validationContext.Ended -= validationContext_Refresh;
                }

                m_context = value; 
                m_validationContext = m_context.As<IValidationContext>();

                if (m_validationContext != null)
                {
                    m_validationContext.Cancelled += validationContext_Refresh;
                    m_validationContext.Ended += validationContext_Refresh;
                }
                
                ContextChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler ContextChanging = delegate { };
        public event EventHandler ContextChanged = delegate { };

        private void validationContext_Refresh(object sender, EventArgs e)
        {
            InvalidateViews();
        }

      
        private object m_context = null;
        public IManipulator Manipulator
        {
            get { return m_manipulator; }
            set
            {                
                m_manipulator = value;
                                
                if (m_manipulator != null)
                {                    
                    Point pt = ActiveView.PointToClient(Control.MousePosition);                    
                    bool picked = m_manipulator.Pick(ActiveView, pt);
                    ActiveView.Cursor = picked ? Cursors.SizeAll : Cursors.Default;                    
                }
                else
                {
                    ActiveView.Cursor = Cursors.Default;
                }
                InvalidateViews();
            }
            

        }

        public IPickFilter PickFilter
        {
            get;
            set;
        }

        public void InvalidateViews()
        {
            foreach (DesignViewControl view in Views)
                view.Invalidate();
        }
      
        /// <summary>
        /// Advances update/render by the given frame time.</summary>
        /// <param name="ft"></param>
        public abstract void Tick(FrameTime ft);

        /// <summary>
        /// Computes frame time and calls
        /// Tick(FrameTime ft) to advance
        /// update/rendering by on tick.
        /// </summary>
        public void Tick()
        {
            FrameTime ft = GetFrameTime();
            Tick(ft);
        }

        /// <summary>
        /// Gets next frame time
        /// Used by Tick() and OnPaint</summary>        
        public FrameTime GetFrameTime()
        {
            long curTick = Stopwatch.GetTimestamp();
            double dt = (double)(curTick - m_lastTicks) / m_frequency;
            m_lastTicks = curTick;
            double TotalTime = (double)(m_lastTicks - m_baseTicks) / m_frequency;
            return  new FrameTime(TotalTime, (float)dt);            
        }
        #endregion

        #region ISnapSettings Members

        public bool SnapVertex { get; set; }
        public bool RotateOnSnap { get; set; }
        public SnapFromMode SnapFrom { get; set; }
        public bool ManipulateLocalAxis { get; set; }

        #endregion

      
        /// <summary>
        /// Gets or sets the background color of the design controls</summary>        
        public Color BackColor
        {
            get { return m_backColor; }
            set
            {
                m_backColor = value;
                foreach (DesignViewControl view in QuadView.Controls)
                {
                    view.BackColor = m_backColor;                         
                }
            }
        }

        /// <summary>
        /// Distance to the camera's far clipping plane.</summary>        
        [DefaultValue(2048.0f)]
        public float CameraFarZ
        {
            get { return m_cameraFarZ; }
            set
            {
                m_cameraFarZ = value;
                foreach (DesignViewControl view in QuadView.Controls)
                {
                    view.Camera.FarZ = m_cameraFarZ;
                }
            }
        }


        /// <summary>
        /// Gets/sets input scheme.</summary>
        [DefaultValue(ControlSchemes.Maya)]
        public ControlSchemes ControlScheme
        {
            get { return m_controlScheme; }
            set
            {
                switch (value)
                {
                    case ControlSchemes.Maya:
                        InputScheme.ActiveControlScheme = new MayaControlScheme();
                        break;
                    case ControlSchemes.MayaLaptop:
                        InputScheme.ActiveControlScheme = new MayaLaptopControlScheme();
                        break;
                    case ControlSchemes.Max:
                        InputScheme.ActiveControlScheme = new MaxControlScheme();
                        break;
                }
                m_controlScheme = value;
            }
        }
        
        /// <summary>
        /// The angle, in degrees, that rotations should be an integer multiple of.</summary>        
        [DefaultValue((float)(5.0f * (Math.PI / 180.0f)))]
        public float SnapAngle
        {
            get
            {
                return (float)(m_SnapAngle * (180.0f / Math.PI)); 
            }
            set
            {
                m_SnapAngle = (float)(value * (Math.PI / 180.0f));                
            }
        }
       
        protected int DefaultSplitterThickness = 8;
        protected QuadPanelControl QuadView;

        #region private members
                
        private float m_cameraFarZ = 2048;
        private ControlSchemes m_controlScheme;
        private float m_SnapAngle;
        private IManipulator m_manipulator;
        private IValidationContext m_validationContext;
        private Color m_backColor = SystemColors.ControlDark;

        // update and render variables.
        private double m_frequency;
        private long m_baseTicks;
        private long m_lastTicks;
        #endregion       
    }
}
