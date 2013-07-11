using System;
using System.Linq;
using System.Collections.Generic;
using Krach.Basics;
using Krach.Extensions;
using Krach.Graphics;
using Kurve.Curves;
using Kurve.Curves.Optimization;
using Krach.Maps.Abstract;
using System.Diagnostics;
using Krach.Maps.Scalar;
using Krach.Maps;
using Kurve.Interface;
using Cairo;

namespace Kurve.Component
{
	class CurveComponent : Component
	{
		readonly CurveOptimizer curveOptimizer;

		readonly List<AnySpecificationComponent> pointSpecificationComponents;
		readonly List<SegmentComponent> segmentComponents;
		readonly FixedPositionComponent curveStartComponent;
		readonly FixedPositionComponent curveEndComponent;

		BasicSpecification nextSpecification;

		BasicSpecification basicSpecification;
		Curve curve;

		bool isShiftDown = false;

		IEnumerable<PositionedControlComponent> PositionedControlComponents
		{
			get
			{
				return Enumerables.Concatenate<PositionedControlComponent>
				(
					pointSpecificationComponents,
					segmentComponents
				);
			}
		}
		IEnumerable<AnySpecificationComponent> SpecificationComponents
		{
			get
			{
				return Enumerables.Concatenate<AnySpecificationComponent>
				(
					pointSpecificationComponents
				);
			}
		}
		IEnumerable<PositionedControlComponent> SegmentDelimitingComponents 
		{
			get 
			{
				return Enumerables.Concatenate<PositionedControlComponent>
				(
					SpecificationComponents,
					Enumerables.Create(curveStartComponent, curveEndComponent)
				);
			}
		}
		IEnumerable<SegmentComponent> SegmentComponents
		{
			get
			{
				return Enumerables.Concatenate<SegmentComponent>
				(
					segmentComponents
				);
			}
		}

		bool Selected
		{
			get
			{
				return
					pointSpecificationComponents.Any(specificationComponent => specificationComponent.Selected) ||
					segmentComponents.Any(segmentComponent => segmentComponent.Selected);
			}
		}

		protected override IEnumerable<Component> SubComponents
		{
			get
			{
				return Enumerables.Concatenate<Component>
				(
					segmentComponents,
					pointSpecificationComponents
				);
			}
		}

		public BasicSpecification BasicSpecification { get { return basicSpecification; } }
		public Curve Curve { get { return curve; } }
		public Specification Specification { get { return curveOptimizer.Specification; } }

		public CurveComponent(Component parent, OptimizationWorker optimizationWorker, Specification specification) : base(parent)
		{
			if (optimizationWorker == null) throw new ArgumentNullException("optimizationWorker");

			this.curveOptimizer = new CurveOptimizer(optimizationWorker, specification);
			this.curveOptimizer.CurveChanged += CurveChanged;

			this.curveStartComponent = new FixedPositionComponent(this, this, 0);
			this.curveEndComponent = new FixedPositionComponent(this, this, 1);
			this.pointSpecificationComponents = new List<AnySpecificationComponent>();
			this.segmentComponents = new List<SegmentComponent>();

			nextSpecification = specification.BasicSpecification;
			curve = null;

			RebuildSegmentComponents();

			curveOptimizer.Submit(nextSpecification);

			IEnumerable<AnySpecificationComponent> specificationComponents = 
				from spec in nextSpecification.CurveSpecifications
				orderby spec.Position ascending
				group spec by spec.Position into specificationGroup
				select new AnySpecificationComponent(this, this, specificationGroup.Key, specificationGroup);

			foreach (AnySpecificationComponent component in specificationComponents) AddSpecificationComponent(component);
		}

		void CurveChanged(BasicSpecification newBasicSpecification, Curve newCurve)
		{
			basicSpecification = newBasicSpecification;
			curve = newCurve;
			
			Changed();
		}

		void InsertLength(double length)
		{
			if (PositionedControlComponents.Any(positionedControlComponent => positionedControlComponent.Selected)) return;

			double newCurveLength = Comparables.Maximum(1, nextSpecification.CurveLength + length);

			ChangeCurveLength(newCurveLength);
			
			curveOptimizer.Submit(nextSpecification);
		}
		void InsertLength(double position, double length)
		{
			double newCurveLength = Comparables.Maximum(1, nextSpecification.CurveLength + length);
			double lengthRatio = nextSpecification.CurveLength / newCurveLength;

			ChangeCurveLength(newCurveLength);

			foreach (SpecificationComponent specificationComponent in SpecificationComponents)
				specificationComponent.CurrentPosition = ShiftPosition(specificationComponent.CurrentPosition, position, lengthRatio);
			
			RebuildCurveSpecification();

			curveOptimizer.Submit(nextSpecification);
		}
		void SpecificationChanged()
		{
			RebuildCurveSpecification();

			curveOptimizer.Submit(nextSpecification);
		}

		void ChangeCurveLength(double newCurveLength)
		{
			nextSpecification = new BasicSpecification
			(
				newCurveLength,
				nextSpecification.SegmentCount,
				nextSpecification.SegmentTemplate,
				nextSpecification.CurveSpecifications
			);
		}
		void RebuildCurveSpecification()
		{
			nextSpecification = new BasicSpecification
			(
				nextSpecification.CurveLength,
				nextSpecification.SegmentCount,
				nextSpecification.SegmentTemplate,
				(
					from specificationComponent in SpecificationComponents
					from specification in specificationComponent.Specifications
					select specification
				)
				.ToArray()
			);
		}

		void AddSpecificationComponent(AnySpecificationComponent specificationComponent)
		{
			specificationComponent.SpecificationChanged += SpecificationChanged;
			specificationComponent.InsertLength += InsertLength;
			specificationComponent.SelectionChanged += SelectionChanged;
			pointSpecificationComponents.Add(specificationComponent);

			RebuildSegmentComponents();

			Changed();

			RebuildCurveSpecification();

			curveOptimizer.Submit(nextSpecification);
		}
		void RemoveSelectedSpecificationComponent(AnySpecificationComponent pointSpecificationComponent)
		{
			pointSpecificationComponents.Remove(pointSpecificationComponent);

			RebuildSegmentComponents();

			Changed();

			RebuildCurveSpecification();

			curveOptimizer.Submit(nextSpecification);
		}

		void AddSpecification(double position)
		{
			AnySpecificationComponent pointSpecificationComponent = new AnySpecificationComponent(this, this, position, Enumerables.Create<CurveSpecification>());

			AddSpecificationComponent(pointSpecificationComponent);
		}

		void RemoveSelectedSpecificationComponent()
		{
			IEnumerable<AnySpecificationComponent> selectedSpecificationComponents =
			(
				from specificationComponent in SpecificationComponents
				where specificationComponent.Selected
				select specificationComponent
			)
			.ToArray();

			foreach (AnySpecificationComponent specificationComponent in selectedSpecificationComponents)
			{
				RemoveSelectedSpecificationComponent(specificationComponent);
			}
		}

		void ChangeCurveSegmentCount(int newSegmentCount)
		{
			if (Selected)
			{
				if (newSegmentCount == 0)
				{
					Console.WriteLine("segment count cannot be zero!");

					return;
				}

				Console.WriteLine("changing segment count to {0}", newSegmentCount);

				nextSpecification = new BasicSpecification
				(
					nextSpecification.CurveLength,
					newSegmentCount,
					nextSpecification.SegmentTemplate,
					nextSpecification.CurveSpecifications
				);
				
				RebuildCurveSpecification();

				curveOptimizer.Submit(nextSpecification);
			}
		}

		void RebuildSegmentComponents()
		{
			IEnumerable<PositionedControlComponent> orderedSpecificationComponents =
			(
				from specificationComponent in SpecificationComponents
				orderby specificationComponent.Position ascending
				select specificationComponent
			)
			.ToArray();

			segmentComponents.Clear();

			IEnumerable<PositionedControlComponent> segmentDelimitingComponents = Enumerables.Concatenate
			(
				Enumerables.Create(curveStartComponent),
				orderedSpecificationComponents,
				Enumerables.Create(curveEndComponent)
			);

			foreach (Tuple<PositionedControlComponent, PositionedControlComponent> segmentDelimitingComponentRange in segmentDelimitingComponents.GetRanges())
			{
				SegmentComponent segmentComponent = new SegmentComponent(this, this, segmentDelimitingComponentRange.Item1, segmentDelimitingComponentRange.Item2);

				segmentComponent.InsertLength += InsertLength;
				segmentComponent.SpecificationChanged += SpecificationChanged;
				segmentComponent.AddSpecification += AddSpecification;
				segmentComponent.SelectionChanged += SelectionChanged;

				segmentComponents.Add(segmentComponent);
			}
		}

		public void SelectionChanged(PositionedControlComponent selectedComponent)
		{
			if (selectedComponent.Selected && !isShiftDown)
			{
				foreach (PositionedControlComponent component in PositionedControlComponents.Except(Enumerables.Create(selectedComponent))) 
					component.Selected = false;

				Changed();
			}
		}

		public override void KeyDown(Key key)
		{
			if (key == Key.Shift) isShiftDown = true;

			base.KeyDown(key);
		}
		public override void KeyUp(Key key)
		{
			switch (key)
			{
				case Key.Shift: isShiftDown = false; break;
				case Key.R: RemoveSelectedSpecificationComponent(); break;
				case Key.Minus: ChangeCurveSegmentCount(nextSpecification.SegmentCount - 1); break;
				case Key.Plus: ChangeCurveSegmentCount(nextSpecification.SegmentCount + 1); break;
			}

			base.KeyUp(key);
		}

		static double ShiftPosition(double position, double insertionPosition, double lengthRatio)
		{
			if (position == insertionPosition) return position;
			if (position < insertionPosition) return position * lengthRatio;
			if (position > insertionPosition) return 1 - (1 - position) * lengthRatio;

			throw new InvalidOperationException();
		}
	}
}

