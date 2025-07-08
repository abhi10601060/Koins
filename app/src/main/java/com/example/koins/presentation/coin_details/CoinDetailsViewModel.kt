package com.example.koins.presentation.coin_details

import android.util.Log
import androidx.compose.runtime.State
import androidx.compose.runtime.mutableStateOf
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.koins.common.Resource
import com.example.koins.domain.use_case.get_coin.GetCoinUseCase
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
import javax.inject.Inject

@HiltViewModel
class CoinDetailsViewModel @Inject constructor(
    private val getCoinUseCase: GetCoinUseCase,
    private val savedStateHandle: SavedStateHandle
) : ViewModel() {

    private val _state = mutableStateOf(CoinDetailsState())
    val state : State<CoinDetailsState> = _state


    fun getCoinDetails(id  : String){
        Log.d("ABHI", "getCoinDetails: " + savedStateHandle.get<String>("id"))
        getCoinUseCase(id).onEach { res ->
            when(res){
                is Resource.Success -> {
                    _state.value = CoinDetailsState(coinDetails = res.data!!)
                }
                is Resource.Error -> {
                    _state.value = CoinDetailsState(message = res.message ?: "Internal server error")
                }
                is Resource.Loading -> {
                    _state.value = CoinDetailsState(isLoading = true)
                }
                else -> {}
            }

        }.launchIn(viewModelScope)
    }
}