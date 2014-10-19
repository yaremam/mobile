﻿using System;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Reports;
using Toggl.Ross.Theme;

namespace Toggl.Ross.Views.Charting
{
    public class PieChart : UIView, IReportChart, IXYDonutChartDataSource
    {
        public EventHandler AnimationEnded { get; set; }

        public EventHandler AnimationStarted { get; set; }

        public EventHandler GoForwardInterval { get; set; }

        public EventHandler GoBackInterval { get; set; }

        private SummaryReportView _reportView;

        public SummaryReportView ReportView
        {
            get {
                return _reportView;
            } set {
                _reportView = value;

                if (_reportView.Projects == null) {
                    return;
                }

                _reportView.Projects.Sort ((x, y) => string.Compare (x.Project, y.Project, StringComparison.Ordinal)); // TODO check better organization

                var delayNoData = (_reportView.Projects.Count == 0) ? 0.5 : 0;
                var delayData = (_reportView.Projects.Count == 0) ? 0 : 0.5;

                UIView.Animate (0.5, delayNoData, UIViewAnimationOptions.TransitionNone,
                () => {
                    noProjectTextLabel.Alpha = (_reportView.Projects.Count == 0) ? 1 : 0;
                    noProjectTitleLabel.Alpha = (_reportView.Projects.Count == 0) ? 1 : 0;
                },  () => {});

                UIView.Animate (0.5, delayData, UIViewAnimationOptions.TransitionNone,
                () => {
                    moneyLabel.Alpha = (_reportView.Projects.Count == 0) ? 0 : 1;;
                    projectTableView.Alpha = (_reportView.Projects.Count == 0) ? 0 : 1;
                    totalTimeLabel.Alpha = (_reportView.Projects.Count == 0) ? 0 : 1;
                },  () => {
                    grayCircle.Alpha = (_reportView.Projects.Count == 0) ? 1 : 0;
                });

                _reportView.Projects.Sort ((x, y) => string.Compare (x.Project, y.Project, StringComparison.Ordinal));

                if (_reportView.Projects.Count == 0) {

                    grayCircle.Alpha = 1;
                    ProjectList.Clear ();
                    donutChart.ReloadData ();

                } else if (_reportView.Projects.Count >= ProjectList.Count) {

                    for (int i = 0; i < ProjectList.Count; i++) {
                        ProjectList [i] = _reportView.Projects [i];
                    }
                    donutChart.ReloadData ();

                    for (int i = ProjectList.Count; i < _reportView.Projects.Count; i++) {
                        ProjectList.Add (_reportView.Projects [i]);
                    }
                    donutChart.ReloadData ();

                } else if (_reportView.Projects.Count < ProjectList.Count) {

                    for (int i = 0; i < _reportView.Projects.Count; i++) {
                        ProjectList [i] = _reportView.Projects [i];
                    }
                    donutChart.ReloadData ();

                    for (int i = _reportView.Projects.Count; i < ProjectList.Count; i++) {
                        ProjectList.RemoveAt (i);
                    }
                    donutChart.ReloadData ();
                }

                donutChart.UserInteractionEnabled = (ProjectList.Count > 1);
                projectTableView.ReloadData ();

                totalTimeLabel.Text = _reportView.TotalGrand;
                moneyLabel.Text = _reportView.TotalBillale;
            }
        }

        public List<ReportProject> ProjectList;

        public PieChart (RectangleF frame) : base (frame)
        {
            const float pieRadius = 80.0f;
            const float lineStroke = 40f;
            const float padding = 24f;
            const float diameter = pieRadius * 2 + lineStroke;

            ProjectList = new List<ReportProject> ();

            grayCircle = new UIView (new RectangleF (0, 0, frame.Width, diameter + padding));
            grayCircle.Layer.AddSublayer ( CGPathCreateArc ( grayCircle.Center, pieRadius, 0, Math.PI * 2, lineStroke));
            Add (grayCircle);

            donutChart = new XYDonutChart (new RectangleF (0, 0, frame.Width, diameter + padding)) {
                DataSource = this,
                PieRadius = pieRadius,
                DonutLineStroke = lineStroke,
                UserInteractionEnabled = true,
                SelectedSliceStroke = 0,
                ShowPercentage = false,
                StartPieAngle = Math.PI * 3/2,
                ShowLabel = false,
                AnimationSpeed = 1.0f,
                SelectedSliceOffsetRadius = 8f
            };
            Add (donutChart);
            donutChart.DidSelectSliceAtIndex += (sender, e) => projectTableView.SelectRow (NSIndexPath.FromRowSection (e.Index, 0), true, UITableViewScrollPosition.Top);
            donutChart.DidDeselectSliceAtIndex += (sender, e) => projectTableView.DeselectRow (NSIndexPath.FromRowSection (e.Index, 0), true);

            projectTableView = new UITableView (new RectangleF (0, donutChart.Frame.Height, frame.Width, frame.Height - donutChart.Frame.Height));
            projectTableView.RegisterClassForCellReuse (typeof (ProjectReportCell), ProjectReportCell.ProjectReportCellId);
            projectTableView.Source = new ProjectListSource (this);
            projectTableView.RowHeight = lineStroke;
            Add (projectTableView);

            totalTimeLabel = new UILabel (new RectangleF ( 0, 0, donutChart.PieRadius * 2 - donutChart.DonutLineStroke, 20));
            totalTimeLabel.Center = new PointF (donutChart.PieCenter.X, donutChart.PieCenter.Y - 10);
            totalTimeLabel.Apply (Style.ReportsView.DonutTimeLabel);
            Add (totalTimeLabel);

            moneyLabel = new UILabel (new RectangleF ( 0, 0, donutChart.PieRadius * 2 - donutChart.DonutLineStroke, 20));
            moneyLabel.Center = new PointF (donutChart.PieCenter.X, donutChart.PieCenter.Y + 10);
            moneyLabel.Apply (Style.ReportsView.DonutMoneyLabel);
            Add (moneyLabel);

            noProjectTitleLabel = new UILabel ( new RectangleF ( 0, 0, donutChart.PieRadius * 2, 20));
            noProjectTitleLabel.Center = new PointF (donutChart.PieCenter.X, donutChart.PieCenter.Y - 20);
            noProjectTitleLabel.Apply (Style.ReportsView.NoProjectTitle);
            noProjectTitleLabel.Text = "NoDataTitle".Tr ();
            Add (noProjectTitleLabel);

            noProjectTextLabel = new UILabel ( new RectangleF ( 0, 0, donutChart.PieRadius * 2, 35));
            noProjectTextLabel.Center = new PointF (donutChart.PieCenter.X, donutChart.PieCenter.Y + 5 );
            noProjectTextLabel.Apply (Style.ReportsView.DonutMoneyLabel);
            noProjectTextLabel.Lines = 2;
            noProjectTextLabel.Text = "NoDataText".Tr ();
            Add (noProjectTextLabel);

            totalTimeLabel.Alpha = 0;
            moneyLabel.Alpha = 0;
            projectTableView.Alpha = 0;

            float dx = 0;
            float dy = 0;
            float border = 12;

            animator = new UIDynamicAnimator (this);
            //animator.Delegate =
            snapRect = new RectangleF (0, 0, frame.Width, diameter + padding);
            snapPoint = new PointF (snapRect.GetMidX (), snapRect.GetMidY ());
            panGesture = new UIPanGestureRecognizer ((pg) => {
                if ((pg.State == UIGestureRecognizerState.Began || pg.State == UIGestureRecognizerState.Changed) && (pg.NumberOfTouches == 1)) {
                    if (snap != null) {
                        animator.RemoveBehavior (snap);
                    }
                    var p0 = pg.LocationInView (this);
                    if (dx == 0) {
                        dx = p0.X - donutChart.Center.X;
                    }
                    if (dy == 0) {
                        dy = p0.Y - donutChart.Center.Y;
                    }
                    var p1 = new PointF (p0.X - dx, donutChart.Center.Y);
                    if ( p1.X - pieRadius < -pieRadius || p1.X + pieRadius > frame.Width + pieRadius ) {
                        return;
                    }
                    donutChart.Center = p1;
                } else if (pg.State == UIGestureRecognizerState.Ended) {
                    if ( donutChart.Center.X - pieRadius < border &&
                            GoBackInterval != null ) {
                        GoBackInterval.Invoke ( this, new EventArgs());
                    } else if ( donutChart.Center.X + pieRadius > frame.Width - border &&
                                donutChart.Center.X + pieRadius < frame.Width + border &&
                                GoForwardInterval != null ) {
                        GoForwardInterval.Invoke ( this, new EventArgs());
                    }
                    dx = 0;
                    dy = 0;
                    SnapImageIntoPlace ( donutChart.Center);
                }
            });

            donutChart.AddGestureRecognizer (panGesture);
        }

        XYDonutChart donutChart;
        UITableView projectTableView;
        UILabel totalTimeLabel;
        UILabel moneyLabel;
        UILabel noProjectTitleLabel;
        UILabel noProjectTextLabel;
        UIView grayCircle;

        UIPanGestureRecognizer panGesture;
        UIDynamicAnimator animator;
        UIAttachmentBehavior snap;
        RectangleF snapRect;
        PointF snapPoint;


        public void SetSelectedProject (int index)
        {
            if ( donutChart.UserInteractionEnabled) {
                donutChart.SetSliceSelectedAtIndex (index);
            }
        }

        public void SetDeselectedProject (int index)
        {
            if ( donutChart.UserInteractionEnabled) {
                donutChart.SetSliceDeselectedAtIndex (index);
            }
        }

        private CAShapeLayer CGPathCreateArc (PointF center, float radius, double startAngle, double endAngle, float lineStroke)
        {
            var shapeLayer = new CAShapeLayer ();
            var path = new CGPath ();
            path.AddArc (center.X, center.Y, radius, Convert.ToSingle (startAngle), Convert.ToSingle (endAngle), false);
            shapeLayer.Path = path.CopyByStrokingPath (lineStroke, CGLineCap.Butt, CGLineJoin.Miter, 10);
            shapeLayer.FillColor = Color.DonutInactiveGray.CGColor;
            return shapeLayer;
        }

        private void SnapImageIntoPlace (PointF touchPoint)
        {
            if (snapRect.Contains (touchPoint)) {
                if (snap != null) {
                    animator.RemoveBehavior (snap);
                }

                //snap = new UISnapBehavior (
                snap = new UIAttachmentBehavior ( donutChart, snapPoint);
                animator.AddBehavior (snap);
            }
        }

        #region Pie Datasource

        public int NumberOfSlicesInPieChart (XYDonutChart pieChart)
        {
            return ProjectList.Count;
        }

        public float ValueForSliceAtIndex (XYDonutChart pieChart, int index)
        {
            return ProjectList [index].TotalTime;
        }

        public UIColor ColorForSliceAtIndex (XYDonutChart pieChart, int index)
        {
            var hex = ProjectModel.HexColors [ProjectList [index].Color % ProjectModel.HexColors.Length];
            return UIColor.Clear.FromHex (hex);
        }

        public string TextForSliceAtIndex (XYDonutChart pieChart, int index)
        {
            return String.Empty;
        }

        #endregion

        internal class AnimatorDelegate : UIDynamicAnimatorDelegate
        {
            SummaryReportView owner;

            public AnimatorDelegate ( SummaryReportView _owner)
            {
                owner = _owner;
            }

            public override void WillResume (UIDynamicAnimator animator)
            {

            }

            public override void DidPause (UIDynamicAnimator animator)
            {
            }
        }

        internal class ProjectListSource : UITableViewSource
        {
            private readonly PieChart _owner;

            public ProjectListSource (PieChart pieChart)
            {
                _owner = pieChart;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = (ProjectReportCell)tableView.DequeueReusableCell (ProjectReportCell.ProjectReportCellId);
                cell.Data = _owner.ProjectList [indexPath.Row];
                return cell;
            }

            public override int RowsInSection (UITableView tableview, int section)
            {
                return _owner.ProjectList.Count;
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                _owner.SetSelectedProject (indexPath.Row);
            }

            public override void RowDeselected (UITableView tableView, NSIndexPath indexPath)
            {
                _owner.SetDeselectedProject (indexPath.Row);
            }
        }
    }
}