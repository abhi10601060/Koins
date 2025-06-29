package com.example.koins.presentation.coin_list

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.koins.common.Resource
import com.example.koins.domain.use_case.get_coins.GetCoinsUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.DelicateCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
import javax.inject.Inject

@HiltViewModel
class CoinListViewModel @Inject constructor(
    private val getCoinsUseCase: GetCoinsUseCase
)  : ViewModel(){
    private val _coinListState = MutableStateFlow(CoinListState())
    val coinListState : StateFlow<CoinListState> = _coinListState

    init {
        getCoins()
    }

    @OptIn(DelicateCoroutinesApi::class)
    private fun getCoins(){
        getCoinsUseCase().onEach { res ->
            when(res) {
                is Resource.Loading -> {
                    _coinListState.value = CoinListState(isLoading = true)
                }
                is Resource.Success -> {
                    _coinListState.value = CoinListState(coins = res.data ?: emptyList())
                }
                is Resource.Error -> {
                    _coinListState.value = CoinListState(error = res.message ?: "unexpected error occured")
                }
                else ->{
                    _coinListState.value = CoinListState(error = "unexpected error occured")
                }
            }
        }.launchIn(viewModelScope)
    }
}