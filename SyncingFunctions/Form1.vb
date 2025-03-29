Imports System.Data.SqlClient
Imports System.Net
Imports Newtonsoft.Json.Linq
Public Class Form1
    Dim connectionString As String = "Server=localhost\SQLEXPRESS;Database=pos_inventory;User Id=sa;Password=angcuteko;Integrated Security=True;"
    Dim apiUrl As String = "http://localhost:5000/api/products"

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim jsonResponse As String = ""

        Using client As New WebClient()
            Try
                client.Headers.Add("Content-Type", "application/json")
                jsonResponse = client.DownloadString(apiUrl)
            Catch ex As Exception
                MessageBox.Show("Error fetching data: " & ex.Message)
                Return
            End Try
        End Using

        Dim jsonObject As JObject = JObject.Parse(jsonResponse)
        Dim products As JArray = jsonObject("products")


        Using conn As New SqlConnection(connectionString)
            conn.Open()

            For Each item As JObject In products
                Dim id As String = item("Id").ToString()
                Dim barcode As String = item("Barcode").ToString()
                Dim description As String = item("Description").ToString()
                Dim unit As String = "pcs"
                Dim initialQty As Decimal = 5
                Dim qty As Decimal = 1
                Dim supplierPrice As Decimal = Convert.ToDecimal(item("SupplierPrice"))
                Dim retailPrice As Decimal = Convert.ToDecimal(item("RetailPrice"))
                Dim wholesalePrice As Decimal = Convert.ToDecimal(item("WholesalePrice"))
                Dim reorderLevel As Integer = Convert.ToInt32(item("ReorderLevel"))
                Dim remarks As String = item("Remarks").ToString()
                Dim isVat As Integer = Convert.ToInt32(item("IsVat"))

                ' Check if ID already exists
                Dim checkQuery As String = "SELECT COUNT(*) FROM tblProducts WHERE id = @Id"
                Using checkCmd As New SqlCommand(checkQuery, conn)
                    checkCmd.Parameters.Add("@id", SqlDbType.VarChar, 50).Value = id
                    Dim count As String = checkCmd.ExecuteScalar()

                    Dim query As String

                    ' Update if exists, otherwise insert
                    If count > 0 Then
                        query = "UPDATE tblProducts SET barcode=@barcode, description=@description, unit=@unit, initialQty=@initialQty, qty=@qty, supplier_price=@supplier_price, retail_price=@retail_price, wholesale_price=@wholesale_price, reorder=@reorder, vat=@vat, remarks=@remarks WHERE id=@id"
                    Else
                        query = "INSERT INTO tblProducts (id, barcode, description, unit, initialQty, qty, supplier_price, retail_price, wholesale_price, reorder, vat, remarks) VALUES (@id,@barcode, @description, @unit, @initialQty, @qty, @supplier_price, @retail_price, @wholesale_price, @reorder, @vat, @remarks)"
                    End If

                    ' Execute Insert or Update
                    Using cmd As New SqlCommand(query, conn)
                        cmd.Parameters.AddWithValue("@id", id)
                        cmd.Parameters.AddWithValue("@barcode", barcode)
                        cmd.Parameters.AddWithValue("@description", description)
                        cmd.Parameters.AddWithValue("@unit", unit)
                        cmd.Parameters.AddWithValue("@initialQty", initialQty)
                        cmd.Parameters.AddWithValue("@qty", qty)
                        cmd.Parameters.AddWithValue("@supplier_price", supplierPrice)
                        cmd.Parameters.AddWithValue("@retail_price", retailPrice)
                        cmd.Parameters.AddWithValue("@wholesale_price", wholesalePrice)
                        cmd.Parameters.AddWithValue("@reorder", reorderLevel)
                        cmd.Parameters.AddWithValue("@vat", isVat)
                        cmd.Parameters.AddWithValue("@remarks", remarks)

                        cmd.ExecuteNonQuery()
                    End Using
                End Using
            Next

            conn.Close()
        End Using
    End Sub
End Class
