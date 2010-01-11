// Copyright (c) 2009 Alex Fajkowski, Apparent Logic LLC
// C# port Copyright (C) 2010 Miguel de Icaza.
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using MonoTouch.UIKit;
using MonoTouch.CoreAnimation;
using System.Drawing;
using MonoTouch.CoreGraphics;
using System.Threading;

namespace OpenFlowSharp
{
	public interface IOpenFlowDataSource {
		void RequestImage (OpenFlowView view, int index);
		UIImage GetDefaultImage ();
	}
	
	public class OpenFlowView : UIView {
		const float kReflectionFraction = 0.85f;
		const int kCoverBuffer = 6;
		
		IOpenFlowDataSource dataSource;
		UIImage defaultImage = new UIImage ();
		UIScrollView scrollView;
		ItemView selectedCoverView;
		CATransform3D leftTransform, rightTransform;
		
		int lowerVisibleCover, upperVisibleCover, numberOfImages, beginningCover;
		float halfScreenHeight, halfScreenWidth, defaultImageHeight;
		bool isSingleTap, isDoubleTap, isDraggingACover;
		float startPosition;
		
		Dictionary<int,UIImage> coverImages = new Dictionary<int, UIImage> ();
		Dictionary<int,float> coverImageHeights = new Dictionary<int, float> ();
		Dictionary<int,ItemView> onscreenCovers = new Dictionary<int, ItemView> ();
		Stack<ItemView> offscreenCovers = new Stack<ItemView> ();
#region Properties
		public UIImage DefaultImage { 
			get {
				return defaultImage;
			}
			
			set {
				defaultImage.Dispose ();
				defaultImageHeight = value.Size.Height;
				defaultImage = ImageUtils.AddImageReflection (value, kReflectionFraction);
			}
		}
		
		public IOpenFlowDataSource DataSource {
			get {
				return dataSource;
			}
			set {
				dataSource = value;
				Layout ();
			}
		}
		
		public int NumberOfImages {
			get {
				return numberOfImages;
			}
			set {
				numberOfImages = value;
				scrollView.ContentSize = new SizeF ((float)(value * coverSpacing + Bounds.Size.Width), Bounds.Size.Height);
				if (selectedCoverView == null)
					SetSelectedCover (0);
				Layout ();
			}
		}
#endregion

#region Configuration settings
		int coverSpacing = 40;
		int centerCoverOffset = 70;
		float sideCoverAngle = 0.79f;
		int sideCoverPosition = -80;
		
		public int CoverSpacing {
			get {
				return coverSpacing;
			}
			set {
				coverSpacing = value;
				Layout ();
			}
		}
		
		void Layout ()
		{
			if (selectedCoverView == null)
				return;
			
			int lowerBound = Math.Max (-1, selectedCoverView.Number - kCoverBuffer);
			int upperBound = Math.Min (NumberOfImages-1, selectedCoverView.Number + kCoverBuffer);
			
			LayoutCovers (selectedCoverView.Number, lowerBound, upperBound);
			CenterOnSelectedCover (false);
			
		}
#endregion
		
		public OpenFlowView (RectangleF bounds, IOpenFlowDataSource dataSource) : base (bounds)
		{
			if (dataSource == null)
				throw new ArgumentNullException ("dataSource");
			
			this.dataSource = dataSource;
			SetupInitialState ();
			
#if AUTOMATIC_DEMO
			Thread t = new Thread (delegate (object a) {
				for (int i = 1; i < 10; i++){
					Thread.Sleep (2000);
					InvokeOnMainThread (delegate {
						SetSelectedCover (i);
						Console.WriteLine ("Set cover: {0}", i);
						CenterOnSelectedCover (true);
					});
				}
			});
			
			t.Start ();
#endif
		}
		
		public override void AwakeFromNib ()
		{
			SetupInitialState ();
			// TODO: maybe support setting up the dataSource here?
		}
 
		
		void SetupInitialState ()
		{
			if (dataSource != null)
				defaultImage = dataSource.GetDefaultImage ();
			
			scrollView = new UIScrollView (Frame) {
				UserInteractionEnabled = false,
				MultipleTouchEnabled = false,
				AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth
			};
			AddSubview (scrollView);
			
			MultipleTouchEnabled = false;
			UserInteractionEnabled = true;
			AutosizesSubviews = true;

			Layer.Position = new System.Drawing.PointF ((float)(Frame.Size.Width / 2), (float)(Frame.Height / 2));
			
			// Initialize the visible and selected cover range.
			lowerVisibleCover = upperVisibleCover = -1;
			selectedCoverView = null;
			
			// Set up transforms
			UpdateTransforms ();
			
			// Set some perspective
			var sublayerTransform = CATransform3D.Identity;
			sublayerTransform.m34 = -0.01f;
			scrollView.Layer.SublayerTransform = sublayerTransform;
			
			Bounds = Frame;
		}

		void UpdateTransforms ()
		{			
			leftTransform = CATransform3D.Identity;
			leftTransform = leftTransform.Rotate (sideCoverAngle, 0, 1, 0);
			rightTransform = CATransform3D.Identity;
			rightTransform = rightTransform.Rotate (sideCoverAngle, 0, -1, 0);
		}
		
		ItemView CoverForIndex (int index)
		{
			var coverView = DequeueReusableCover ();
			if (coverView == null)
				coverView = new ItemView (this, System.Drawing.RectangleF.Empty);
			coverView.Number = index;
			
			return coverView;
		}
			
		void UpdateCoverImage (ItemView aCover)
		{
			UIImage coverImage;
			if (coverImages.TryGetValue (aCover.Number, out coverImage)){
				if (coverImageHeights.ContainsKey (aCover.Number))
					aCover.SetImage (coverImage, coverImageHeights [aCover.Number], kReflectionFraction);
			} else {
				aCover.SetImage (defaultImage, defaultImageHeight, kReflectionFraction);
				if (dataSource != null)
					dataSource.RequestImage (this, aCover.Number);
			}
		}
		
		ItemView DequeueReusableCover ()
		{
			if (offscreenCovers.Count > 0)
				return offscreenCovers.Pop ();
			return null;
		}
		
		void LayoutCover (ItemView aCover, int selectedIndex, bool animated)
		{
			int coverNumber = aCover.Number;
			CATransform3D newTransform;
			float newZPosition = sideCoverPosition;
			PointF newPosition = new PointF (halfScreenWidth + aCover.HorizontalPosition, halfScreenHeight + aCover.VerticalPosition);
			
			if (coverNumber < selectedIndex){
				newPosition.X -= centerCoverOffset;
				newTransform = leftTransform;
			} else if (coverNumber > selectedIndex){
				newPosition.X += centerCoverOffset;
				newTransform = rightTransform;
			} else {
				newZPosition = 0;
				newTransform = CATransform3D.Identity;
			}
			
			if (animated){
				BeginAnimations (null);
				SetAnimationCurve (UIViewAnimationCurve.EaseOut);
				SetAnimationBeginsFromCurrenState (true);
			}
			aCover.Layer.Transform = newTransform;
			aCover.Layer.ZPosition = newZPosition;
			aCover.Layer.Position = newPosition;
			
			if (animated)
				CommitAnimations ();
		}
		
		void LayoutCovers (int selected, int from, int to)
		{
			for (int i = from; i <= to; i++){
				ItemView cover;
				
				if (onscreenCovers.TryGetValue (i, out cover))
					LayoutCover (cover, selected, true);
			}
		}
		
		ItemView FindCoversOnScreen (CALayer targetLayer)
		{
			foreach (ItemView cover in onscreenCovers.Values)
				if (cover.ImageView.Layer == targetLayer)
					return cover;
			return null;
		}
		
		protected override void Dispose (bool disposing)
		{
			if (disposing){
				defaultImage.Dispose ();
				scrollView.Dispose ();
				foreach (var k in offscreenCovers)
					k.Dispose ();
				foreach (var k in onscreenCovers.Values)
					k.Dispose ();
			}
			
			base.Dispose (disposing);
		}

		public override RectangleF Bounds {
			get {
				return base.Bounds;
			}
			set {
				base.Bounds = value;
				halfScreenHeight = value.Size.Height/2;
				halfScreenWidth = value.Size.Width/2;
				
				Layout ();
			}
		}

		public void SetSelectedCover (int newSelectedCover)
		{
			if (selectedCoverView != null && (newSelectedCover == selectedCoverView.Number))
			    return;
			    
			int newLowerBound = Math.Max (0, newSelectedCover - kCoverBuffer);
			int newUpperBound = Math.Min (numberOfImages - 1, newSelectedCover + kCoverBuffer);
			if (selectedCoverView == null){
				// Allocate and display covers from newLower to newUYpper bounds.
				for (int i = newLowerBound; i <= newUpperBound; i++){
					var cover = CoverForIndex (i);
					onscreenCovers [i] = cover;
					UpdateCoverImage (cover);
					scrollView.Layer.AddSublayer (cover.Layer);
					LayoutCover (cover, newSelectedCover, false);
				}
				
				lowerVisibleCover = newLowerBound;
				upperVisibleCover = newUpperBound;
				selectedCoverView = onscreenCovers [newSelectedCover];
				return;
			}
			
			// Check to see if the new and current ranges overlap
			if ((newLowerBound > upperVisibleCover) || (newUpperBound < lowerVisibleCover)){
				// They do not overlap at all
				// This does not animate -- assuming it's programmatically set from view controller.
				// Recycle all onscreen covers.
				for (int i = lowerVisibleCover; i <= upperVisibleCover; i++){
					var cover = onscreenCovers [i];
					offscreenCovers.Push (cover);
					cover.RemoveFromSuperview ();
					onscreenCovers.Remove (i);
				}
				
				// Move all available covers to new location
				for (int i = newLowerBound; i <= newUpperBound; i++){
					var cover = CoverForIndex (i);
					onscreenCovers [i] = cover;
					UpdateCoverImage (cover);
					scrollView.Layer.AddSublayer (cover.Layer);
				}
				lowerVisibleCover = newLowerBound;
				upperVisibleCover = newUpperBound;
				selectedCoverView = onscreenCovers [newSelectedCover];
				LayoutCovers (newSelectedCover, newLowerBound, newUpperBound);
				return;
			} else if (newSelectedCover > selectedCoverView.Number){
				for (int i = lowerVisibleCover; i < newLowerBound; i++){
					var cover = onscreenCovers [i];
					if (upperVisibleCover < newUpperBound){
						// Tack it on right side
						upperVisibleCover++;
						cover.Number = upperVisibleCover;
						UpdateCoverImage (cover);
						onscreenCovers [cover.Number] = cover;
						LayoutCover (cover, newSelectedCover, false);
					} else {
						// Recycle this cover
						offscreenCovers.Push (cover);
						cover.RemoveFromSuperview ();
					}
					onscreenCovers.Remove (i);
				}
				lowerVisibleCover = newLowerBound;
				
				// Add in any missing covers on the right up to the newUpperBound.
				for (int i = upperVisibleCover+1; i <= newUpperBound; i++){
					var cover = CoverForIndex (i);
					onscreenCovers [i] = cover;
					UpdateCoverImage (cover);
					scrollView.Layer.AddSublayer (cover.Layer);
					LayoutCover (cover, newSelectedCover, false);
				}
				upperVisibleCover = newUpperBound;
			} else {
				// Move covers that are now out of range on the right to the left side.
				// but only if appropriate (within the range set by newLoweBound).
				for (int i = upperVisibleCover; i > newUpperBound; i--){
					var cover = onscreenCovers [i];
					if (lowerVisibleCover > newLowerBound){
						// Tack it on the left
						lowerVisibleCover--;
						cover.Number = lowerVisibleCover;
						UpdateCoverImage (cover);
						onscreenCovers [lowerVisibleCover] = cover;
						LayoutCover (cover, newSelectedCover, false);
					} else {
						// Recycle this cover
						offscreenCovers.Push (cover);
						cover.RemoveFromSuperview ();
					}
				}
				upperVisibleCover = newUpperBound;
				
				// Add in any missing covers on the left down to the newLowerBound
				for (int i = lowerVisibleCover - 1; i >= newLowerBound; i--){
					var cover = CoverForIndex (i);
					onscreenCovers [i] = cover;
					UpdateCoverImage (cover);
					scrollView.Layer.AddSublayer (cover.Layer);
					LayoutCover (cover, newSelectedCover, false);
				}
				lowerVisibleCover = newLowerBound;
			}
				
			if (selectedCoverView.Number > newSelectedCover)
				LayoutCovers (newSelectedCover, newSelectedCover, selectedCoverView.Number);
			else if (newSelectedCover > selectedCoverView.Number)
				LayoutCovers (newSelectedCover, selectedCoverView.Number, newSelectedCover);
			
			selectedCoverView = onscreenCovers [newSelectedCover];
		}
		
		public void CenterOnSelectedCover (bool animated)
		{
			var selectedOffset = new PointF ((float) coverSpacing * selectedCoverView.Number, 0);
			scrollView.SetContentOffset (selectedOffset, animated);
		}
		
		public int Selected { 
			get {
				if (selectedCoverView == null)
					return -1;
				
				return selectedCoverView.Number;
			}
		}
		
		public UIImage this [int idx] {
			get {
				return coverImages [idx];
			}
			set {
				var imageWithReflection = ImageUtils.AddImageReflection (value, kReflectionFraction);
				coverImages [idx] = imageWithReflection;
				coverImageHeights [idx] = value.Size.Height;
				
				// If the image is onscreen, set its image and call layoutCover
				ItemView aCover;
				
				if (onscreenCovers.TryGetValue (idx, out aCover)){
					aCover.SetImage (imageWithReflection, value.Size.Height, kReflectionFraction);
					LayoutCover (aCover, selectedCoverView.Number, false);
				}
			}
		}
			
		public override void TouchesBegan (MonoTouch.Foundation.NSSet touches, UIEvent evt)
 	 	{
			var startPoint = ((UITouch) touches.AnyObject).LocationInView (this);
			isDraggingACover = false;
			
			// Which cover did the user tap?
			var targetLayer = scrollView.Layer.HitTest (startPoint);
			var targetCover = FindCoversOnScreen (targetLayer);
			isDraggingACover = targetCover != null;
			
			beginningCover = selectedCoverView.Number;
			// Make sure the user is tapping on a cover.
			startPosition = (float)((startPoint.X / 1.5) + scrollView.ContentOffset.X);
			
			if (isSingleTap)
				isDoubleTap = true;
			
			isSingleTap = touches.Count == 1;
		}

		public override void TouchesMoved (MonoTouch.Foundation.NSSet touches, UIEvent evt)
		{
			isSingleTap = false;
			isDoubleTap = false;
			
			// Only scroll if the user started on a cover
			if (!isDraggingACover)
				return;
			
			var movedPoint = ((UITouch) touches.AnyObject).LocationInView (this);
			var offset = startPosition - (movedPoint.X / 1.5);
			var newPoint = new PointF ((float) offset, 0);
			scrollView.ContentOffset = newPoint;
			int newCover = (int)(offset / coverSpacing);
			if (newCover != selectedCoverView.Number){
				if (newCover < 0)
					SetSelectedCover (0);
				else if (newCover >= numberOfImages)
					SetSelectedCover (numberOfImages-1);
				else
					SetSelectedCover (newCover);
			}
		}
		
		public event EventHandler Changed;
		
		public override void TouchesEnded (MonoTouch.Foundation.NSSet touches, UIEvent evt)
		{
			if (isSingleTap){
				var targetPoint = ((UITouch)touches.AnyObject).LocationInView (this);
				var targetLayer = scrollView.Layer.HitTest (targetPoint);
				var targetCover = FindCoversOnScreen (targetLayer);
				
				if (targetCover != null && (targetCover.Number != selectedCoverView.Number))
					SetSelectedCover (targetCover.Number);
			}
			CenterOnSelectedCover (true);
			
			// Raise the event
			if (beginningCover != selectedCoverView.Number){
				EventHandler h = Changed;
				if (h != null)
					h (this, EventArgs.Empty);
			}
		}
	

	}
}
