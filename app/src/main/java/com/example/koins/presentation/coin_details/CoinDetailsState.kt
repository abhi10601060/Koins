package com.example.koins.presentation.coin_details

import com.example.koins.domain.model.CoinDetails

data class CoinDetailsState(
    val isLoading : Boolean = false,
    val coinDetails : CoinDetails? = null,
    val message : String = ""
)
