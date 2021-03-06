﻿using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace LottieUWP
{
    internal class RectangleContent : IPathContent
    {
        private readonly Path _path = new Path();
        private Rect _rect;

        private readonly LottieDrawable _lottieDrawable;
        private readonly IBaseKeyframeAnimation<PointF> _positionAnimation;
        private readonly IBaseKeyframeAnimation<PointF> _sizeAnimation;
        private readonly IBaseKeyframeAnimation<float?> _cornerRadiusAnimation;

        private TrimPathContent _trimPath;
        private bool _isPathValid;

        internal RectangleContent(LottieDrawable lottieDrawable, BaseLayer layer, RectangleShape rectShape)
        {
            Name = rectShape.Name;
            _lottieDrawable = lottieDrawable;
            _positionAnimation = rectShape.Position.CreateAnimation();
            _sizeAnimation = rectShape.Size.CreateAnimation();
            _cornerRadiusAnimation = rectShape.CornerRadius.CreateAnimation();

            layer.AddAnimation(_positionAnimation);
            layer.AddAnimation(_sizeAnimation);
            layer.AddAnimation(_cornerRadiusAnimation);

            _positionAnimation.ValueChanged += OnValueChanged;
            _sizeAnimation.ValueChanged += OnValueChanged;
            _cornerRadiusAnimation.ValueChanged += OnValueChanged;
        }

        public string Name { get; }

        private void OnValueChanged(object sender, EventArgs eventArgs)
        {
            Invalidate();
        }

        private void Invalidate()
        {
            _isPathValid = false;
            _lottieDrawable.InvalidateSelf();
        }

        public void SetContents(IList<IContent> contentsBefore, IList<IContent> contentsAfter)
        {
            for (var i = 0; i < contentsBefore.Count; i++)
            {
                if (contentsBefore[i] is TrimPathContent trimPathContent && trimPathContent.Type == ShapeTrimPath.Type.Simultaneously)
                {
                    _trimPath = trimPathContent;
                    _trimPath.ValueChanged += OnValueChanged;
                }
            }
        }

        public Path Path
        {
            get
            {
                if (_isPathValid)
                {
                    return _path;
                }

                _path.Reset();

                var size = _sizeAnimation.Value;
                var halfWidth = size.X / 2f;
                var halfHeight = size.Y / 2f;
                var radius = _cornerRadiusAnimation?.Value ?? 0f;
                var maxRadius = Math.Min(halfWidth, halfHeight);
                if (radius > maxRadius)
                {
                    radius = maxRadius;
                }

                // Draw the rectangle top right to bottom left.
                var position = _positionAnimation.Value;

                _path.MoveTo(position.X + halfWidth, position.Y - halfHeight + radius);

                _path.LineTo(position.X + halfWidth, position.Y + halfHeight - radius);

                if (radius > 0)
                {
                    RectExt.Set(ref _rect, position.X + halfWidth - 2 * radius, position.Y + halfHeight - 2 * radius, position.X + halfWidth, position.Y + halfHeight);
                    _path.ArcTo(_rect, 0, 90);
                }

                _path.LineTo(position.X - halfWidth + radius, position.Y + halfHeight);
                
                if (radius > 0)
                {
                    RectExt.Set(ref _rect, position.X - halfWidth, position.Y + halfHeight - 2 * radius, position.X - halfWidth + 2 * radius, position.Y + halfHeight);
                    _path.ArcTo(_rect, 90, 90);
                }
                
                _path.LineTo(position.X - halfWidth, position.Y - halfHeight + radius);
                
                if (radius > 0)
                {
                    RectExt.Set(ref _rect, position.X - halfWidth, position.Y - halfHeight, position.X - halfWidth + 2 * radius, position.Y - halfHeight + 2 * radius);
                    _path.ArcTo(_rect, 180, 90);
                }
                
                _path.LineTo(position.X + halfWidth - radius, position.Y - halfHeight);
                
                if (radius > 0)
                {
                    RectExt.Set(ref _rect, position.X + halfWidth - 2 * radius, position.Y - halfHeight, position.X + halfWidth, position.Y - halfHeight + 2 * radius);
                    _path.ArcTo(_rect, 270, 90);
                }
                _path.Close();

                Utils.ApplyTrimPathIfNeeded(_path, _trimPath);

                _isPathValid = true;
                return _path;
            }
        }
    }
}