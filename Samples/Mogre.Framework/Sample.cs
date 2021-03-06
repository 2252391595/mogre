﻿// Alimer - Copyright (C) Amer Koleci
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#if INCLUDE_RTSHADER_SYSTEM
using Mogre.RTShader;
#endif
using System;
using System.Windows.Forms;

namespace Mogre.Framework
{
	/// <summary>
	/// Base class for samples
	/// </summary>
	public abstract class Sample : Form
	{
		protected readonly FileSystemLayer _fileSystemLayer;
		protected Root _root;
		protected OverlaySystem _overlaySystem;
		protected RenderWindow _window;
		protected SceneManager _sceneManager;
		protected Camera _camera;
		protected CompositorWorkspace _workspace;
		protected ColourValue _backgroundColor = ColourValue.Black;
		protected SdkTrayManager _trayManager;
		protected int _textureMode = 0;
		protected int _renderMode = 0;

		public Camera Camera
		{
			get { return _camera; }
		}

		public SceneManager SceneManager
		{
			get { return _sceneManager; }
		}

		public SdkTrayManager TrayManager
		{
			get { return _trayManager; }
		}

		protected Sample()
		{
			_fileSystemLayer = new FileSystemLayer();
			SuspendLayout();
			ClientSize = new System.Drawing.Size(800, 600);
			FormBorderStyle = FormBorderStyle.Fixed3D;
			MaximizeBox = false;
			Name = "OgreWindow";
			StartPosition = FormStartPosition.CenterScreen;
			Text = "Mogre Render Window";
			ResumeLayout(false);
		}

		public virtual bool Setup()
		{
			// Create root
			var pluginFileName = _fileSystemLayer.GetConfigFilePath("plugins.cfg");
			_root = new Root(pluginFileName,
				_fileSystemLayer.GetWritablePath("ogre.cfg"),
				_fileSystemLayer.GetWritablePath("Ogre.log"));

			// Create OverlaySystem
			_overlaySystem = new OverlaySystem();

			// 
			SetupResources();
			//if (Configure() == false)
			//{
			//	return false;
			//}
			SetupDirectX();

			// Create RenderWindow
			_root.Initialise(false);
			var nameValuePairList = new NameValuePairList();
			nameValuePairList["externalWindowHandle"] = Handle.ToString();
			_window = _root.CreateRenderWindow("Mogre RenderWindow", 800, 600, false, nameValuePairList);

			TestCapabilities(_root.RenderSystem.Capabilities);

			// Now we have GPU stuff setup
			ResourceGroupManager.Singleton.AddBuiltinLocations();

			_sceneManager = CreateSceneManager();
#if INCLUDE_RTSHADER_SYSTEM
			if (!_root.RenderSystem.Capabilities.HasCapability(Capabilities.RSC_FIXED_FUNCTION))
			{
				InitializeRTShaderSystem(_sceneManager);
			}
#endif

			_overlaySystem.SceneManager = _sceneManager;
			CreateCamera();
			TextureManager.Singleton.DefaultNumMipmaps = 5;
			LoadResources();
			_workspace = SetupCompositor();
			CreateInputHandler();
			Disposed += OgreWindow_Disposed;
			CreateScene();

			// Create Frame Listeners
			_root.FrameStarted += OnFrameStarted;
			_root.FrameRenderingQueued += OnFrameRenderingQueued;
			_root.FrameEnded += OnFrameEnded;
			_sceneManager.RenderQueueStarted += _sceneManager_RenderQueueStarted;
			_trayManager = new SdkTrayManager("SampleControls", _window);
			_trayManager.ShowFrameStats(TrayLocation.BottomLeft);
			_trayManager.ShowLogo(TrayLocation.BottomRight);
			_trayManager.ShowCursor();
			
			return true;
		}

		private void _sceneManager_RenderQueueStarted(byte queueGroupId, string invocation, out bool skipThisInvocation)
		{
			skipThisInvocation = false;
		}

		protected virtual bool OnFrameStarted(FrameEvent evt)
		{
			return true;
		}

		protected virtual bool OnFrameRenderingQueued(FrameEvent evt)
		{
			// quit if window was closed
			if (_window.IsDisposed || _window.IsClosed)
				return false;

			_trayManager.Update(evt.timeSinceLastFrame);

			return true;
		}

		protected virtual bool OnFrameEnded(FrameEvent evt)
		{
			return true;
		}

		protected virtual void TestCapabilities(RenderSystemCapabilities caps)
		{

		}

		protected virtual void SetupResources()
		{
			using (var configFile = new ConfigFile())
			{
				configFile.Load(_fileSystemLayer.GetConfigFilePath("resources.cfg"));
				ConfigFile.SectionIterator sectionIterator = configFile.GetSectionIterator();
				while (sectionIterator.MoveNext())
				{
					string currentKey = sectionIterator.CurrentKey;
					foreach (var pair in sectionIterator.Current)
					{
						ResourceGroupManager.Singleton.AddResourceLocation(pair.Value, pair.Key, currentKey);
					}
				}
			}
		}

		protected virtual DefaultInputHandler CreateInputHandler()
		{
			return new DefaultInputHandler(this);
		}

		protected virtual SceneManager CreateSceneManager()
		{
#if DEBUG
			//Debugging multithreaded code is a PITA, disable it.
			const int numThreads = 1;
			InstancingThreadedCullingMethod threadedCullingMethod = InstancingThreadedCullingMethod.SingleThread;
#else
			// GetNumLogicalCores() may return 0 if couldn't detect
			var numThreads = System.Math.Max(1, PlatformInformation.NumLogicalCores);

			InstancingThreadedCullingMethod threadedCullingMethod = InstancingThreadedCullingMethod.SingleThread;

			//See doxygen documentation regarding culling methods.
			//In some cases you may still want to use single thread.
			if (numThreads > 1)
				threadedCullingMethod = InstancingThreadedCullingMethod.Threaded;
#endif

			return _root.CreateSceneManager(SceneType.Generic, numThreads, threadedCullingMethod);
		}

		protected virtual void CreateCamera()
		{
			_camera = _sceneManager.CreateCamera("PlayerCam");
			_camera.Position = new Vector3(0f, 0f, 500f);
			_camera.LookAt(new Vector3(0f, 0f, -300f));
			_camera.AutoAspectRatio = true;
			_camera.NearClipDistance = 5.0f;
		}

		protected virtual CompositorWorkspace SetupCompositor()
		{
			CompositorManager2 compositorManager = _root.CompositorManager2;
			string workspaceName = GetType().Name + "Workspace";
			if (!compositorManager.HasWorkspaceDefinition(workspaceName))
			{
				compositorManager.CreateBasicWorkspaceDef(workspaceName, _backgroundColor);
			}

			return compositorManager.AddWorkspace(_sceneManager, _window, _camera, workspaceName);
		}

#if INCLUDE_RTSHADER_SYSTEM
		protected virtual bool InitializeRTShaderSystem(SceneManager sceneMgr)
		{
			if (ShaderGenerator.Initialize())
			{
				ShaderGenerator.Singleton.AddSceneManager(sceneMgr);
			}

			return true;
		}
#endif


		protected virtual void LoadResources()
		{
			ResourceGroupManager.Singleton.InitialiseAllResourceGroups();
		}

		public virtual void Run()
		{
			if (!Setup())
			{
				return;
			}
			Show();

			while (_root != null && _root.RenderOneFrame())
			{
				Application.DoEvents();
			}
		}

		void Shutdown()
		{
			DestroyScene();

			Utilities.Dispose(ref _workspace);
			_root.CompositorManager2.RemoveAllWorkspaces();
			Utilities.Dispose(ref _overlaySystem);
			Utilities.Dispose(ref _window);
			Utilities.Dispose(ref _camera);
			_root.DestroySceneManager(_sceneManager);
			Utilities.Dispose(ref _sceneManager);

			_root.FrameStarted -= OnFrameStarted;
			_root.FrameRenderingQueued -= OnFrameRenderingQueued;
			_root.FrameEnded -= OnFrameEnded;
			Utilities.Dispose(ref _root);
		}

		void OgreWindow_Disposed(object sender, EventArgs e)
		{
			Shutdown();
		}

		void SetupDirectX()
		{
			RenderSystem renderSystemByName = _root.GetRenderSystemByName("Direct3D9 Rendering Subsystem");
			_root.RenderSystem = renderSystemByName;
			renderSystemByName.SetConfigOption("Full Screen", "No");
			renderSystemByName.SetConfigOption("Video Mode", "800 x 600 @ 32-bit colour");
		}

		protected virtual bool Configure()
		{
			if (_root.RestoreConfig() || _root.ShowConfigDialog())
			{
				return true;
			}

			return false;
		}

		protected abstract void CreateScene();

		protected virtual void DestroyScene()
		{

		}

		protected internal void TakeScreenshot()
		{
			string[] temp = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "screenshot*.jpg");
			string fileName = string.Format("screenshot{0}.jpg", temp.Length + 1);

			TakeScreenshot(fileName);
		}

		protected internal void TakeScreenshot(string fileName)
		{
			_window.WriteContentsToFile(fileName);
		}

		protected internal void CycleTextureFilteringMode()
		{
			_textureMode = (_textureMode + 1) % 4;
			switch (_textureMode)
			{
				case 0:
					MaterialManager.Singleton.SetDefaultTextureFiltering(TextureFilterOptions.TFO_BILINEAR);
					break;

				case 1:
					MaterialManager.Singleton.SetDefaultTextureFiltering(TextureFilterOptions.TFO_TRILINEAR);
					break;

				case 2:
					MaterialManager.Singleton.SetDefaultTextureFiltering(TextureFilterOptions.TFO_ANISOTROPIC);
					MaterialManager.Singleton.DefaultAnisotropy = 8;
					//_debugOverlay.AdditionalInfo = "Anisotropic";
					break;

				case 3:
					MaterialManager.Singleton.SetDefaultTextureFiltering(TextureFilterOptions.TFO_NONE);
					MaterialManager.Singleton.DefaultAnisotropy = 1;
					break;
			}
		}

		protected internal void CyclePolygonMode()
		{
			_renderMode = (_renderMode + 1) % 3;
			switch (_renderMode)
			{
				case 0:
					_camera.PolygonMode = PolygonMode.PM_SOLID;
					break;

				case 1:
					_camera.PolygonMode = PolygonMode.PM_WIREFRAME;
					break;

				case 2:
					_camera.PolygonMode = PolygonMode.PM_POINTS;
					break;
			}
		}

		public static void ShowOgreException()
		{
			// TODO
		}
	}
}
