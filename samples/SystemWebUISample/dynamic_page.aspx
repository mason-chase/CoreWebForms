<%@ Page Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="dynamic_page.aspx.cs" Inherits="SystemWebUISample.Pages.DynamicPage" %>

<script runat="server">
    protected void Page_PreInit(object sender, EventArgs e)
    {
    }

    protected void Button1_Click(object sender, EventArgs e)
    {
        label2.Text = GetText("Button1");
    }

    protected void Button2_Click(object sender, EventArgs e)
    {
        label2.Text = GetText("Button2");
    }
</script>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
  <asp:Label id="label" Text="First Name" runat="server" />
  <asp:TextBox id="txt" runat="server" Value="" />

  <hr />

  <asp:Button id="button1" value="Go" runat="server" Text="Click Me" OnClick="Button1_Click" />
  <asp:Button id="button2" value="Go" runat="server" Text="Click Me" OnClick="Button2_Click" />

  Button clicked: <b><asp:Label id="label2" Text="None" runat="server" /></b>

<%--
     <asp:RequiredFieldValidator runat="server" ControlToValidate="Name" Display="Dynamic"
            CssClass="field-validation-valid text-danger" ErrorMessage="The Name field is required." />
--%>
</asp:Content>

