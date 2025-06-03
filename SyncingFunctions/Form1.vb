Imports System.Data.SqlClient
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class Form1
    Dim connectionString As String = "Server=localhost\SQLEXPRESS;Database=pos_inventory;User Id=sa;Password=angcuteko;Integrated Security=True;"
    Dim apiUrl As String = "https://pos-backend-api-1-6s4f.onrender.com"
    Private authToken As String = ""
    Private Async Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim credentials = ShowLoginForm()
        If credentials Is Nothing Then
            MessageBox.Show("Sync canceled. You must log in first.", "Login Required", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Call login API with credentials
        Dim token = Await LoginAsync(credentials.Item1, credentials.Item2)
        If String.IsNullOrEmpty(token) Then
            MessageBox.Show("Login failed. Please check your username and password.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        ' Store token for other API calls
        authToken = token
        SyncExpensesToApi(authToken)
        SyncCartToApi(authToken)
        SyncCheckInventoryToApi(authToken)

        MessageBox.Show("Data synced successfully!", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)

    End Sub

    Private Function ShowLoginForm() As Tuple(Of String, String)
        Dim loginForm As New Form() With {
            .Text = "Login Required",
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .StartPosition = FormStartPosition.CenterParent,
            .Size = New Size(320, 220),
            .MaximizeBox = False,
            .MinimizeBox = False,
            .ShowInTaskbar = False
        }

        Dim lblUsername As New Label() With {
            .Text = "Username:",
            .Location = New Point(20, 20),
            .AutoSize = True
        }

        Dim txtUsername As New TextBox() With {
            .Location = New Point(100, 20),
            .Width = 180
        }

        Dim lblPassword As New Label() With {
            .Text = "Password:",
            .Location = New Point(20, 60),
            .AutoSize = True
        }

        Dim txtPassword As New TextBox() With {
            .Location = New Point(100, 60),
            .Width = 180,
            .UseSystemPasswordChar = True
        }

        Dim btnContinue As New Button() With {
            .Text = "Continue",
            .Location = New Point(35, 110),
            .Width = 250,
            .Height = 35,
            .BackColor = ColorTranslator.FromHtml("#254D70"),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .DialogResult = DialogResult.OK,
            .Padding = New Padding(5)
        }

        loginForm.AcceptButton = btnContinue

        loginForm.Controls.Add(lblUsername)
        loginForm.Controls.Add(txtUsername)
        loginForm.Controls.Add(lblPassword)
        loginForm.Controls.Add(txtPassword)
        loginForm.Controls.Add(btnContinue)

        If loginForm.ShowDialog() = DialogResult.OK Then
            Return Tuple.Create(txtUsername.Text, txtPassword.Text)
        Else
            Return Nothing
        End If
    End Function

    ' Separate async function to call login API
    Private Async Function LoginAsync(username As String, password As String) As Task(Of String)
        Dim loginUrl As String = "https://pos-backend-api-1-6s4f.onrender.com/api/users/login"

        Using client As New HttpClient()
            Dim payload As New JObject()
            payload("username") = username
            payload("password") = password

            Dim content As New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

            Try
                Dim response As HttpResponseMessage = Await client.PostAsync(loginUrl, content)
                Dim responseString As String = Await response.Content.ReadAsStringAsync()

                If response.IsSuccessStatusCode Then
                    Dim jsonResponse As JObject = JObject.Parse(responseString)
                    If jsonResponse("message") IsNot Nothing AndAlso jsonResponse("message").ToString().ToLower().Contains("successful") Then
                        Return jsonResponse("token").ToString()
                    Else
                        Return ""
                    End If
                Else
                    Return ""
                End If

            Catch ex As Exception
                Return ""
            End Try
        End Using
    End Function

    Private Function SyncExpensesToApi(Optional bearerToken As String = "") As Boolean
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = "SELECT description, amount, remarks, date, addedBy FROM tblExpenses"
                Using cmd As New SqlCommand(query, conn)
                    Using reader As SqlDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim expenseData As New Dictionary(Of String, Object) From {
                            {"description", reader("description").ToString()},
                            {"amount", Convert.ToDecimal(reader("amount"))},
                            {"remarks", reader("remarks").ToString()},
                            {"date", Convert.ToDateTime(reader("date")).ToString("yyyy-MM-dd")},
                            {"addedBy", reader("addedBy").ToString()}
                        }

                            Dim jsonPayload As String = JsonConvert.SerializeObject(expenseData)

                            Using client As New WebClient()
                                client.Headers(HttpRequestHeader.ContentType) = "application/json"
                                If Not String.IsNullOrEmpty(bearerToken) Then
                                    client.Headers(HttpRequestHeader.Authorization) = "Bearer " & bearerToken
                                End If

                                Dim response As String = client.UploadString("https://pos-backend-api-1-6s4f.onrender.com/api/expenses/create", "POST", jsonPayload)
                                Console.WriteLine("Synced: " & jsonPayload)
                            End Using
                        End While
                    End Using
                End Using
            End Using

            MessageBox.Show("Expenses synced successfully!", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return True
        Catch ex As Exception
            MessageBox.Show("Error during sync: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    Private Function SyncCartToApi(Optional bearerToken As String = "") As Boolean
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = "SELECT transaction_type, invoice, pid, vat, supplier_price, markup_price, qty, discountPercent, discount, vatSales, vatAmount, vatExempt, sub_total, date, cashier, status, bankSales, creditSales, gcashSales, cashSales FROM tblCart"
                Using cmd As New SqlCommand(query, conn)
                    Using reader As SqlDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim cartData As New Dictionary(Of String, Object) From {
                                {"transactionType", reader("transaction_type").ToString()},
                                {"invoice", If(IsDBNull(reader("invoice")), 0, Convert.ToInt32(reader("invoice")))},
                                {"pid", If(IsDBNull(reader("pid")), 0, Convert.ToInt32(reader("pid")))},
                                {"vat", If(IsDBNull(reader("vat")), False, Convert.ToBoolean(reader("vat")))},
                                {"supplierPrice", Convert.ToDecimal(reader("supplier_price"))},
                                {"markupPrice", Convert.ToDecimal(reader("markup_price"))},
                                {"qty", Convert.ToDecimal(reader("qty"))},
                                {"discountPercent", Convert.ToDecimal(reader("discountPercent"))},
                                {"discount", Convert.ToDecimal(reader("discount"))},
                                {"vatSales", Convert.ToDecimal(reader("vatSales"))},
                                {"vatAmount", Convert.ToDecimal(reader("vatAmount"))},
                                {"vatExempt", Convert.ToDecimal(reader("vatExempt"))},
                                {"subTotal", Convert.ToDecimal(reader("sub_total"))},
                                {"date", Convert.ToDateTime(reader("date")).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")},
                                {"cashier", reader("cashier").ToString()},
                                {"status", reader("status").ToString()},
                                {"bankSales", Convert.ToDecimal(reader("bankSales"))},
                                {"creditSales", Convert.ToDecimal(reader("creditSales"))},
                                {"gcashSales", Convert.ToDecimal(reader("gcashSales"))},
                                {"cashSales", Convert.ToDecimal(reader("cashSales"))}
                            }

                            Dim jsonPayload As String = JsonConvert.SerializeObject(cartData)

                            Using client As New WebClient()
                                client.Headers(HttpRequestHeader.ContentType) = "application/json"
                                If Not String.IsNullOrEmpty(bearerToken) Then
                                    client.Headers(HttpRequestHeader.Authorization) = "Bearer " & bearerToken
                                End If

                                Dim response As String = client.UploadString("https://pos-backend-api-1-6s4f.onrender.com/api/cart/create", "POST", jsonPayload)
                                Console.WriteLine("Synced Cart: " & jsonPayload)
                            End Using
                        End While
                    End Using
                End Using
            End Using

            MessageBox.Show("Cart synced successfully!", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return True
        Catch ex As Exception
            MessageBox.Show("Error syncing Cart: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    Private Function SyncCheckInventoryToApi(Optional bearerToken As String = "") As Boolean
        Try
            Using conn As New SqlConnection(connectionString)
                conn.Open()
                Dim query As String = "SELECT reference, pid, systemInventory, actualInventory, date, username, status FROM tblCheckInventory"
                Using cmd As New SqlCommand(query, conn)
                    Using reader As SqlDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim invData As New Dictionary(Of String, Object) From {
                            {"reference", reader("reference").ToString()},
                            {"pid", reader("pid").ToString()},
                            {"systemInventory", Convert.ToDecimal(reader("systemInventory"))},
                            {"actualInventory", Convert.ToDecimal(reader("actualInventory"))},
                            {"date", Convert.ToDateTime(reader("date")).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")},
                            {"username", reader("username").ToString()},
                            {"status", reader("status").ToString()}
                        }

                            Dim jsonPayload As String = JsonConvert.SerializeObject(invData)

                            Using client As New WebClient()
                                client.Headers(HttpRequestHeader.ContentType) = "application/json"
                                If Not String.IsNullOrEmpty(bearerToken) Then
                                    client.Headers(HttpRequestHeader.Authorization) = "Bearer " & bearerToken
                                End If

                                Dim response As String = client.UploadString("https://pos-backend-api-1-6s4f.onrender.com/api/check-inventory/create", "POST", jsonPayload)
                                Console.WriteLine("Synced CheckInventory: " & jsonPayload)
                            End Using
                        End While
                    End Using
                End Using
            End Using

            MessageBox.Show("CheckInventory synced successfully!", "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return True
        Catch ex As Exception
            MessageBox.Show("Error syncing CheckInventory: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function



End Class
