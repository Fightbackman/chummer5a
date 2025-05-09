/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chummer
{
    public partial class PrintMultipleCharacters : Form
    {
        private CancellationTokenSource _objPrinterCancellationTokenSource;
        private readonly CancellationTokenSource _objGenericCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _objGenericToken;
        private Task _tskPrinter;
        private Character[] _aobjCharacters;
        private CharacterSheetViewer _frmPrintView;

        #region Control Events

        public PrintMultipleCharacters()
        {
            Disposed += (sender, args) =>
            {
                _objGenericCancellationTokenSource.Dispose();
                dlgOpenFile?.Dispose();
            };
            _objGenericToken = _objGenericCancellationTokenSource.Token;
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
        }

        private async void PrintMultipleCharacters_Load(object sender, EventArgs e)
        {
            try
            {
                dlgOpenFile.Title = await LanguageManager.GetStringAsync("Title_PrintMultiple", token: _objGenericToken).ConfigureAwait(false);
                dlgOpenFile.Filter = await LanguageManager.GetStringAsync("DialogFilter_Chummer", token: _objGenericToken).ConfigureAwait(false) + '|' +
                                     await LanguageManager.GetStringAsync("DialogFilter_Chum5", token: _objGenericToken).ConfigureAwait(false) + '|' +
                                     await LanguageManager.GetStringAsync("DialogFilter_Chum5lz", token: _objGenericToken).ConfigureAwait(false) + '|' +
                                     await LanguageManager.GetStringAsync("DialogFilter_All", token: _objGenericToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //swallow this
            }
        }

        private async void PrintMultipleCharacters_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancellationTokenSource objTemp = Interlocked.Exchange(ref _objPrinterCancellationTokenSource, null);
            if (objTemp?.IsCancellationRequested == false)
            {
                objTemp.Cancel(false);
                objTemp.Dispose();
            }
            _objGenericCancellationTokenSource.Cancel(false);
            // ReSharper disable once MethodSupportsCancellation
            await CleanUpOldCharacters(CancellationToken.None).ConfigureAwait(false);
        }

        private async void cmdSelectCharacter_Click(object sender, EventArgs e)
        {
            // Add the selected Files to the list of characters to print.
            if (await this.DoThreadSafeFuncAsync(x => dlgOpenFile.ShowDialog(x), token: _objGenericToken).ConfigureAwait(false) != DialogResult.OK)
                return;
            CancellationTokenSource objNewSource = new CancellationTokenSource();
            CancellationToken objToken = objNewSource.Token;
            CancellationTokenSource objTemp = Interlocked.Exchange(ref _objPrinterCancellationTokenSource, objNewSource);
            if (objTemp?.IsCancellationRequested == false)
            {
                objTemp.Cancel(false);
                objTemp.Dispose();
            }
            Task tskOld = Interlocked.Exchange(ref _tskPrinter, null);
            if (tskOld?.IsCompleted == false)
            {
                try
                {
                    await tskOld.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //swallow this
                }
            }
            try
            {
                await cmdPrint.DoThreadSafeAsync(x => x.Enabled = true, _objGenericToken).ConfigureAwait(false);
                await prgProgress.DoThreadSafeAsync(x => x.Value = 0, _objGenericToken).ConfigureAwait(false);

                foreach (string strFileName in dlgOpenFile.FileNames)
                {
                    TreeNode objNode = new TreeNode
                    {
                        Text = Path.GetFileName(strFileName) ?? await LanguageManager.GetStringAsync("String_Unknown", token: _objGenericToken).ConfigureAwait(false),
                        Tag = strFileName
                    };
                    await treCharacters.DoThreadSafeAsync(x => x.Nodes.Add(objNode), _objGenericToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                objNewSource.Dispose();
                return;
            }

            if (_frmPrintView != null)
            {
                Task tskNew = Task.Run(() => DoPrint(objToken), objToken);
                if (Interlocked.CompareExchange(ref _tskPrinter, tskNew, null) != null)
                {
                    Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                    try
                    {
                        objNewSource.Cancel(false);
                    }
                    finally
                    {
                        objNewSource.Dispose();
                    }
                    try
                    {
                        await tskNew.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        //swallow this
                    }
                }
            }
            else
            {
                Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                objNewSource.Dispose();
            }
        }

        private async void cmdDelete_Click(object sender, EventArgs e)
        {
            try
            {
                TreeNode objSelectedNode = await treCharacters
                                                 .DoThreadSafeFuncAsync(x => x.SelectedNode, _objGenericToken)
                                                 .ConfigureAwait(false);
                if (objSelectedNode != null)
                {
                    CancellationTokenSource objNewSource = new CancellationTokenSource();
                    CancellationToken objToken = objNewSource.Token;
                    CancellationTokenSource objTemp = Interlocked.Exchange(ref _objPrinterCancellationTokenSource, objNewSource);
                    if (objTemp?.IsCancellationRequested == false)
                    {
                        objTemp.Cancel(false);
                        objTemp.Dispose();
                    }
                    Task tskOld = Interlocked.Exchange(ref _tskPrinter, null);
                    if (tskOld?.IsCompleted == false)
                    {
                        try
                        {
                            await tskOld.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            //swallow this
                        }
                    }
                    try
                    {
                        await cmdPrint.DoThreadSafeAsync(x => x.Enabled = true, _objGenericToken).ConfigureAwait(false);
                        await prgProgress.DoThreadSafeAsync(x => x.Value = 0, _objGenericToken).ConfigureAwait(false);
                        await treCharacters.DoThreadSafeAsync(() => objSelectedNode.Remove(), _objGenericToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                        objNewSource.Dispose();
                        return;
                    }
                    catch
                    {
                        Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                        objNewSource.Dispose();
                        throw;
                    }

                    if (_frmPrintView != null)
                    {
                        Task tskNew = Task.Run(() => DoPrint(objToken), objToken);
                        if (Interlocked.CompareExchange(ref _tskPrinter, tskNew, null) != null)
                        {
                            Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                            try
                            {
                                objNewSource.Cancel(false);
                            }
                            finally
                            {
                                objNewSource.Dispose();
                            }
                            try
                            {
                                await tskNew.ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                //swallow this
                            }
                        }
                    }
                    else
                    {
                        Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                        objNewSource.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //swallow this
            }
        }

        private async void cmdPrint_Click(object sender, EventArgs e)
        {
            CancellationTokenSource objNewSource = new CancellationTokenSource();
            CancellationToken objToken = objNewSource.Token;
            CancellationTokenSource objTemp = Interlocked.Exchange(ref _objPrinterCancellationTokenSource, objNewSource);
            if (objTemp?.IsCancellationRequested == false)
            {
                objTemp.Cancel(false);
                objTemp.Dispose();
            }
            Task tskOld = Interlocked.Exchange(ref _tskPrinter, null);
            if (tskOld?.IsCompleted == false)
            {
                try
                {
                    await tskOld.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //swallow this
                }
            }
            try
            {
                await cmdPrint.DoThreadSafeAsync(x => x.Enabled = true, _objGenericToken).ConfigureAwait(false);
                await prgProgress.DoThreadSafeAsync(x => x.Value = 0, _objGenericToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                objNewSource.Dispose();
                return;
            }
            catch
            {
                Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                objNewSource.Dispose();
                throw;
            }
            Task tskNew = Task.Run(() => DoPrint(objToken), objToken);
            if (Interlocked.CompareExchange(ref _tskPrinter, tskNew, null) != null)
            {
                Interlocked.CompareExchange(ref _objPrinterCancellationTokenSource, null, objNewSource);
                try
                {
                    objNewSource.Cancel(false);
                }
                finally
                {
                    objNewSource.Dispose();
                }
                try
                {
                    await tskNew.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //swallow this
                }
            }
        }

        private async Task DoPrint(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            CursorWait objCursorWait = await CursorWait.NewAsync(this, true, token).ConfigureAwait(false);
            try
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    int intNodesCount = await treCharacters.DoThreadSafeFuncAsync(x => x.Nodes.Count, token).ConfigureAwait(false);
                    await cmdPrint.DoThreadSafeAsync(x => x.Enabled = false, token).ConfigureAwait(false);
                    await prgProgress.DoThreadSafeAsync(objBar =>
                    {
                        objBar.Value = 0;
                        objBar.Maximum = intNodesCount;
                    }, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    // Parallelized load because this is one major bottleneck.
                    Character[] lstCharacters = new Character[intNodesCount];
                    Task<Character>[] tskLoadingTasks = new Task<Character>[intNodesCount];
                    for (int i = 0; i < tskLoadingTasks.Length; ++i)
                    {
                        int i1 = i;
                        string strLoopFile
                            = await treCharacters.DoThreadSafeFuncAsync(x => x.Nodes[i1].Tag.ToString(), token).ConfigureAwait(false);
                        tskLoadingTasks[i]
                            = Task.Run(() => InnerLoad(strLoopFile, token), token);
                    }

                    async Task<Character> InnerLoad(string strLoopFile, CancellationToken innerToken = default)
                    {
                        innerToken.ThrowIfCancellationRequested();

                        Character objReturn;
                        using (ThreadSafeForm<LoadingBar> frmLoadingBar
                               = await Program.CreateAndShowProgressBarAsync(strLoopFile, Character.NumLoadingSections, token: innerToken).ConfigureAwait(false))
                            objReturn = await Program.LoadCharacterAsync(
                                strLoopFile, string.Empty, false, false, frmLoadingBar.MyForm, innerToken).ConfigureAwait(false);
                        bool blnLoadSuccessful = objReturn != null;
                        innerToken.ThrowIfCancellationRequested();

                        if (blnLoadSuccessful)
                            await prgProgress.DoThreadSafeAsync(x => ++x.Value, innerToken).ConfigureAwait(false);
                        return objReturn;
                    }

                    await Task.WhenAll(tskLoadingTasks).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    for (int i = 0; i < lstCharacters.Length; ++i)
                        lstCharacters[i] = await tskLoadingTasks[i].ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    await CleanUpOldCharacters(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    _aobjCharacters = lstCharacters;

                    if (_frmPrintView == null)
                    {
                        _frmPrintView = await this.DoThreadSafeFuncAsync(x =>
                        {
                            CharacterSheetViewer objReturn = new CharacterSheetViewer();
                            x.Disposed += (sender, args) => objReturn.Dispose();
                            return objReturn;
                        }, token).ConfigureAwait(false);
                        await _frmPrintView.SetSelectedSheet("Game Master Summary", token).ConfigureAwait(false);
                        await _frmPrintView.SetCharacters(token, _aobjCharacters).ConfigureAwait(false);
                        await _frmPrintView.DoThreadSafeAsync(x => x.Show(), token).ConfigureAwait(false);
                    }
                    else
                    {
                        await _frmPrintView.SetCharacters(token, _aobjCharacters).ConfigureAwait(false);
                        await _frmPrintView.DoThreadSafeAsync(x => x.Activate(), token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await cmdPrint.DoThreadSafeAsync(x => x.Enabled = true, token).ConfigureAwait(false);
                    await prgProgress.DoThreadSafeAsync(x => x.Value = 0, token).ConfigureAwait(false);
                }
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task CleanUpOldCharacters(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!(_aobjCharacters?.Length > 0))
                return;
            // Dispose of any characters who were previous loaded but are no longer needed and don't have any linked characters
            bool blnAnyChanges = true;
            while (blnAnyChanges)
            {
                token.ThrowIfCancellationRequested();
                blnAnyChanges = false;
                foreach (Character objCharacter in _aobjCharacters)
                {
                    if (!await Program.OpenCharacters.ContainsAsync(objCharacter, token: token).ConfigureAwait(false)
                        || await Program.OpenCharacters.AnyAsync(async x => (await x.GetLinkedCharactersAsync(token).ConfigureAwait(false)).Contains(objCharacter), token).ConfigureAwait(false)
                        || Program.MainForm.OpenFormsWithCharacters.Any(x => x.CharacterObjects.Contains(objCharacter)))
                        continue;
                    blnAnyChanges = true;
                    await Program.OpenCharacters.RemoveAsync(objCharacter, token: token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                }
            }
        }

        #endregion Control Events
    }
}
