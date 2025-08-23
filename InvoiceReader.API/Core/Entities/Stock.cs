using System.ComponentModel;

namespace InvoiceReader.API.Core.Entities;

public class Stock
{
    [Description("Ticker da ação. Se terminar com a letra F, remover a última letra")]
    public string Ticker { get; set; }

    [Description("Quantidade de ações. Caso seja uma compra 'c' o valor deve ser positivo, caso seja uma venda 'v' o valor deve vir negativo")]
    public int Amount { get; set; }

    [Description("Preço unitário da ação")]
    public double Price { get; set; }
}
