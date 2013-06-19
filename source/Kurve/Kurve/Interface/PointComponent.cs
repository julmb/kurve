using System;
using System.Linq;
using System.Collections.Generic;
using Krach.Basics;
using Krach.Extensions;
using Wrappers.Casadi;
using Cairo;
using Krach.Graphics;

namespace Kurve.Interface
{
	class PointComponent : Component
	{
		static readonly Vector2Double size = new Vector2Double(10, 10);

		double position;
		Vector2Double point;
		bool mouseDown;
		bool dragging;
		bool selected;

		Orthotope2Double Bounds { get { return new Orthotope2Double(point - 0.5 * size, point + 0.5 * size); } }

		public double Position { get { return position; } }
		public Vector2Double Point { get { return point; } }
		public bool Selected { get { return selected; } }

		public PointComponent(double position, Vector2Double point)
		{
			this.position = position;
			this.point = point;
			this.mouseDown = false;
			this.dragging = false;
			this.selected = false;
			
			OnUpdate();
		}

		public override void Draw(Context context)
		{
			context.Rectangle(Bounds.Start.X + 0.5, Bounds.Start.Y + 0.5, Bounds.Size.X - 1, Bounds.Size.Y - 1);
			
			context.LineWidth = 1;
			context.LineCap = LineCap.Butt;
			context.Color = InterfaceUtility.ToCairoColor(Colors.Black);

			if (selected) context.Fill();
			else context.Stroke();
		}
		public override void MouseDown(Vector2Double mousePosition, MouseButton mouseButton)
		{
			if (Bounds.Contains(mousePosition) && mouseButton == MouseButton.Left)
			{
				mouseDown = true;

				OnUpdate();
			}
		}
		public override void MouseUp(Vector2Double mousePosition, MouseButton mouseButton)
		{
			if (mouseDown)
			{
				if (mouseButton == MouseButton.Left)
				{
					if (!dragging) selected = !selected;
					mouseDown = false;
					dragging = false;
				}
				
				OnUpdate();
			}
		}
		public override void MouseMove(Vector2Double mousePosition)
		{
			if (mouseDown) 
			{
				point = mousePosition;
				dragging = true;
				
				OnUpdate();
			}	
		}
		public override void Scroll(ScrollDirection scrollDirection)
		{
			if (selected)
			{
				switch (scrollDirection)
				{
					case ScrollDirection.Up: position -= 0.01; break;
					case ScrollDirection.Down: position += 0.01; break;
					default: throw new ArgumentException();
				}

				position = position.Clamp(0, 1);

				OnUpdate();
			}
		}
	}
}

