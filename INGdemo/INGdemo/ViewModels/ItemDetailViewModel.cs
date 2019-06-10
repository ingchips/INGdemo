using System;

using INGota.Models;
using INGdemo.Models;

namespace INGota.ViewModels
{
    public class ItemDetailViewModel : BaseViewModel
    {
        public BLEDev Item { get; set; }
        public ItemDetailViewModel(BLEDev item = null)
        {
            Item = item;
            Title = item?.Name;
        }
    }
}
