using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;

using MauiDesigner.Core.Protocol;

using Forms = System.Windows.Forms;

namespace MauiDesigner.Vsix
{
    public sealed class GridDefinitionsEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(
            ITypeDescriptorContext? context) =>
            UITypeEditorEditStyle.Modal;

        public override object? EditValue(
            ITypeDescriptorContext? context,
            IServiceProvider provider,
            object? value)
        {
            string propertyName = context?.PropertyDescriptor?.Name ?? "Grid definitions";
            string? serializedValue = value switch
            {
                DesignerGridDefinitionValue definitions =>
                    definitions.SerializedValue,
                string text => text,
                _ => null
            };
            if (!DesignerGridDefinitions.TryParse(
                    serializedValue,
                    out IReadOnlyList<DesignerGridTrack> tracks))
            {
                Forms.MessageBox.Show(
                    "The current Grid definition is invalid. Correct it in XAML before using the visual editor.",
                    propertyName,
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Warning);
                return value;
            }

            using var dialog = new GridDefinitionsDialog(propertyName, tracks);
            return dialog.ShowDialog() == Forms.DialogResult.OK
                ? new DesignerGridDefinitionValue(dialog.SerializedValue)
                : value;
        }
    }

    internal sealed class GridDefinitionsDialog : Forms.Form
    {
        private readonly Forms.DataGridView _grid;
        private readonly bool _rows;

        public GridDefinitionsDialog(
            string propertyName,
            IReadOnlyList<DesignerGridTrack> tracks)
        {
            _rows = propertyName.Equals("RowDefinitions", StringComparison.Ordinal);
            Text = _rows ? "Edit Grid Rows" : "Edit Grid Columns";
            Width = 560;
            Height = 390;
            MinimumSize = new System.Drawing.Size(460, 300);
            StartPosition = Forms.FormStartPosition.CenterParent;
            FormBorderStyle = Forms.FormBorderStyle.SizableToolWindow;
            ShowInTaskbar = false;
            AutoScaleMode = Forms.AutoScaleMode.Dpi;

            _grid = CreateGrid();
            BuildLayout();
            LoadValue(tracks);
        }

        public string SerializedValue
        {
            get
            {
                var tracks = new List<DesignerGridTrack>(_grid.Rows.Count);
                foreach (Forms.DataGridViewRow row in _grid.Rows)
                {
                    string unitText = Convert.ToString(
                        row.Cells["Unit"].Value,
                        CultureInfo.InvariantCulture) ?? "Star";
                    if (!Enum.TryParse(
                            unitText,
                            out DesignerGridUnitType unit))
                    {
                        unit = DesignerGridUnitType.Star;
                    }
                    double numeric = 1;
                    if (unit != DesignerGridUnitType.Auto)
                    {
                        string value = Convert.ToString(
                            row.Cells["Value"].Value,
                            CultureInfo.CurrentCulture) ?? string.Empty;
                        if (!TryParseNonNegative(value, out numeric))
                        {
                            throw new InvalidOperationException(
                                "The Grid definition was not validated.");
                        }
                    }
                    tracks.Add(new DesignerGridTrack(unit, numeric));
                }

                return DesignerGridDefinitions.Serialize(tracks);
            }
        }

        private Forms.DataGridView CreateGrid()
        {
            var grid = new Forms.DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = System.Drawing.SystemColors.Window,
                BorderStyle = Forms.BorderStyle.FixedSingle,
                Dock = Forms.DockStyle.Fill,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = Forms.DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn
            {
                Name = "Track",
                HeaderText = _rows ? "Row" : "Column",
                ReadOnly = true,
                FillWeight = 35
            });
            grid.Columns.Add(new Forms.DataGridViewComboBoxColumn
            {
                Name = "Unit",
                HeaderText = "Sizing",
                DataSource = Enum.GetNames(typeof(DesignerGridUnitType)),
                FillWeight = 65
            });
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn
            {
                Name = "Value",
                HeaderText = "Weight / pixels",
                FillWeight = 80
            });
            grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["Unit"].Index)
                {
                    UpdateValueCell(grid.Rows[e.RowIndex]);
                }
            };
            grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (grid.IsCurrentCellDirty)
                {
                    grid.CommitEdit(Forms.DataGridViewDataErrorContexts.Commit);
                }
            };
            return grid;
        }

        private void BuildLayout()
        {
            var instruction = new Forms.Label
            {
                AutoSize = true,
                Dock = Forms.DockStyle.Fill,
                Padding = new Forms.Padding(0, 0, 0, 8),
                Text = "Add tracks, choose Auto, Star, or Absolute sizing, then set the weight or pixel value."
            };

            var add = new Forms.Button { Text = _rows ? "Add row" : "Add column", AutoSize = true };
            var remove = new Forms.Button { Text = "Remove", AutoSize = true };
            var up = new Forms.Button { Text = "Move up", AutoSize = true };
            var down = new Forms.Button { Text = "Move down", AutoSize = true };
            add.Click += (_, _) => AddTrack();
            remove.Click += (_, _) => RemoveSelected();
            up.Click += (_, _) => MoveSelected(-1);
            down.Click += (_, _) => MoveSelected(1);

            var tools = new Forms.FlowLayoutPanel
            {
                AutoSize = true,
                Dock = Forms.DockStyle.Fill,
                FlowDirection = Forms.FlowDirection.LeftToRight,
                Padding = new Forms.Padding(0, 8, 0, 8),
                WrapContents = false
            };
            tools.Controls.AddRange(new Forms.Control[] { add, remove, up, down });

            var ok = new Forms.Button
            {
                Text = "OK",
                DialogResult = Forms.DialogResult.None,
                AutoSize = true
            };
            var cancel = new Forms.Button
            {
                Text = "Cancel",
                DialogResult = Forms.DialogResult.Cancel,
                AutoSize = true
            };
            ok.Click += (_, _) =>
            {
                if (ValidateTracks())
                {
                    DialogResult = Forms.DialogResult.OK;
                    Close();
                }
            };

            var actions = new Forms.FlowLayoutPanel
            {
                AutoSize = true,
                Dock = Forms.DockStyle.Fill,
                FlowDirection = Forms.FlowDirection.RightToLeft,
                Padding = new Forms.Padding(0, 8, 0, 0),
                WrapContents = false
            };
            actions.Controls.AddRange(new Forms.Control[] { cancel, ok });

            var layout = new Forms.TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = Forms.DockStyle.Fill,
                Padding = new Forms.Padding(12),
                RowCount = 4
            };
            layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
            layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
            layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.AutoSize));
            layout.Controls.Add(instruction, 0, 0);
            layout.Controls.Add(tools, 0, 1);
            layout.Controls.Add(_grid, 0, 2);
            layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadValue(IReadOnlyList<DesignerGridTrack> tracks)
        {
            foreach (DesignerGridTrack track in tracks)
            {
                AddTrack(track);
            }
        }

        private void AddTrack() =>
            AddTrack(new DesignerGridTrack(DesignerGridUnitType.Star, 1));

        private void AddTrack(DesignerGridTrack track)
        {
            int index = _grid.Rows.Add(
                _grid.Rows.Count + 1,
                track.UnitType.ToString(),
                track.UnitType == DesignerGridUnitType.Auto
                    ? string.Empty
                    : track.Value.ToString("G", CultureInfo.InvariantCulture));
            UpdateValueCell(_grid.Rows[index]);
            _grid.Rows[index].Selected = true;
        }

        private void RemoveSelected()
        {
            int index = SelectedIndex();
            if (index < 0)
            {
                return;
            }

            _grid.Rows.RemoveAt(index);
            Renumber();
        }

        private void MoveSelected(int offset)
        {
            int index = SelectedIndex();
            int destination = index + offset;
            if (index < 0 || destination < 0 || destination >= _grid.Rows.Count)
            {
                return;
            }

            object? unit = _grid.Rows[index].Cells["Unit"].Value;
            object? value = _grid.Rows[index].Cells["Value"].Value;
            _grid.Rows.RemoveAt(index);
            _grid.Rows.Insert(destination, destination + 1, unit, value);
            UpdateValueCell(_grid.Rows[destination]);
            _grid.Rows[destination].Selected = true;
            Renumber();
        }

        private int SelectedIndex() =>
            _grid.SelectedRows.Count == 0 ? -1 : _grid.SelectedRows[0].Index;

        private void Renumber()
        {
            for (int index = 0; index < _grid.Rows.Count; index++)
            {
                _grid.Rows[index].Cells["Track"].Value = index + 1;
            }
        }

        private static void UpdateValueCell(Forms.DataGridViewRow row)
        {
            bool isAuto = string.Equals(
                Convert.ToString(row.Cells["Unit"].Value, CultureInfo.InvariantCulture),
                DesignerGridUnitType.Auto.ToString(),
                StringComparison.Ordinal);
            Forms.DataGridViewCell valueCell = row.Cells["Value"];
            valueCell.ReadOnly = isAuto;
            valueCell.Style.BackColor = isAuto
                ? System.Drawing.SystemColors.Control
                : System.Drawing.SystemColors.Window;
            if (isAuto)
            {
                valueCell.Value = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(Convert.ToString(valueCell.Value)))
            {
                valueCell.Value = "1";
            }
        }

        private bool ValidateTracks()
        {
            _grid.EndEdit();
            foreach (Forms.DataGridViewRow row in _grid.Rows)
            {
                string unit = Convert.ToString(
                    row.Cells["Unit"].Value,
                    CultureInfo.InvariantCulture) ?? string.Empty;
                if (unit == DesignerGridUnitType.Auto.ToString())
                {
                    continue;
                }

                string value = Convert.ToString(
                    row.Cells["Value"].Value,
                    CultureInfo.InvariantCulture) ?? string.Empty;
                if (!TryParseNonNegative(value, out _))
                {
                    Forms.MessageBox.Show(
                        this,
                        "Star weights and absolute pixel values must be non-negative numbers.",
                        Text,
                        Forms.MessageBoxButtons.OK,
                        Forms.MessageBoxIcon.Warning);
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseNonNegative(string value, out double number)
        {
            bool parsed = double.TryParse(
                              value,
                              NumberStyles.Float,
                              CultureInfo.CurrentCulture,
                              out number) ||
                          double.TryParse(
                              value,
                              NumberStyles.Float,
                              CultureInfo.InvariantCulture,
                              out number);
            return parsed &&
                !double.IsNaN(number) &&
                !double.IsInfinity(number) &&
                number >= 0;
        }
    }
}
