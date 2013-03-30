using System;
using Krach.Basics;
using Krach.Calculus.Terms;
using Krach.Calculus;

namespace Kurve.Curves.Specification
{
	public class PointCurveSpecification : CurveSpecification
	{
		readonly double position;
		readonly Vector2Double point;

		public override double Position { get { return position; } }
		
		public PointCurveSpecification(double position, Vector2Double point)
		{
			if (position < 0 || position > 1) throw new ArgumentOutOfRangeException("position");
			
			this.position = position;
			this.point = point;
		}

		public override ValueTerm GetErrorTerm(Curve curve)
		{
			return Term.NormSquared(Term.Difference(curve.Function.Apply(Term.Constant(position)), Term.Constant(point.X, point.Y)));
		}
	}
}
