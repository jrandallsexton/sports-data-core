SELECT *
  FROM [sdProvider.Development].[dbo].[ResourceIndex] ri
  where ri.IsEnabled = 1
  order by ri.ordinal

  --delete from [sdProvider.Development].[dbo].[ResourceIndex]

  --update [sdProvider.Development].[dbo].[ResourceIndex] set IsEnabled = 0 where Id != '126CDE67-F5EA-461A-9F28-00888F6513A4'